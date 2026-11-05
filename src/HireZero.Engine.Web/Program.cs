using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HireZero.Core;
using HireZero.Integrations.Inference;
using Microsoft.AspNetCore.RateLimiting;

namespace HireZero.Engine.Web;

public static class WebHost
{
    public static async Task Main(string[] args)
    {
        var options = WebOptions.Parse(args);
        await using var app = Build(
            options,
            options.Mode == "gallery" ? null : Environment.GetEnvironmentVariable("HIREZERO_WEB_PASSPHRASE")
        );
        await app.RunAsync();
    }

    public static WebApplication Build(WebOptions options, string? passphrase)
    {
        if (
            options.Mode == "worker"
            && options.Origin?.IsLoopback != true
            && string.Equals(
                Environment.GetEnvironmentVariable(InferenceSettings.ProviderVariable),
                InferenceSettings.OpenRouter,
                StringComparison.OrdinalIgnoreCase
            )
        )
            throw new ArgumentException("OpenRouter is restricted to a private HTTPS loopback worker.");
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { Args = [], ApplicationName = typeof(WebHost).Assembly.GetName().Name }
        );
        // Only this configured listener may exist, even when an ambient Kestrel endpoint or ASPNETCORE_URLS is set.
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Limits.MaxRequestBodySize = 8192;
            k.AddServerHeader = false;
            k.Listen(IPAddress.Loopback, options.Port);
        });
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        );
        builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        builder.Services.AddRateLimiter(r =>
        {
            r.OnRejected = (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                return new(
                    context.HttpContext.Response.WriteAsJsonAsync(
                        new { error = "Too many requests. Wait before trying again." },
                        token
                    )
                );
            };
            void Add(string name, int limit) =>
                r.AddFixedWindowLimiter(
                    name,
                    o =>
                    {
                        o.PermitLimit = limit;
                        o.Window = TimeSpan.FromMinutes(1);
                        o.QueueLimit = 0;
                        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    }
                );
            // Per client, so a stranger spending login attempts cannot lock the owner out. The passphrase's
            // 16-character minimum, not this limit, is what makes guessing it impractical.
            r.AddPolicy(
                "login",
                ctx =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        LoginClient(ctx),
                        _ =>
                            new()
                            {
                                PermitLimit = 5,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                            }
                    )
            );
            Add("paid", 12);
        });
        var app = builder.Build();
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (
            options.Data is { } data && (Files.Inside(data, webRoot) || Files.Inside(webRoot, data))
            || options.Gallery is { } galleryPath
                && (Files.Inside(galleryPath, webRoot) || Files.Inside(webRoot, galleryPath))
        )
            throw new ArgumentException("Gallery and private records must be separate from static assets.");
        app.Use(
            async (ctx, next) =>
            {
                var response = ctx.Response;
                response.Headers.CacheControl = "no-store";
                response.Headers["Referrer-Policy"] = "no-referrer";
                response.Headers["X-Content-Type-Options"] = "nosniff";
                response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self'; script-src 'self'; style-src 'self'";
                if (!options.Accepts(ctx.Request))
                {
                    response.StatusCode = 400;
                    await response.WriteAsJsonAsync(new { error = "Unexpected host or HTTPS proxy scheme." });
                    return;
                }
                try
                {
                    await next();
                }
                catch (Exception e) when (!response.HasStarted && e is not OutOfMemoryException)
                {
                    var (status, message) = e switch
                    {
                        WorkspaceException w => (w.Status, w.Message),
                        BadHttpRequestException b => (b.StatusCode, "Invalid or oversized request."),
                        InferenceConfigurationException => (503, "The operator must configure the inference provider."),
                        _ => (
                            500,
                            "The operation could not be completed. Inspect saved evidence before starting another job."
                        ),
                    };
                    response.StatusCode = status;
                    await response.WriteAsJsonAsync(new { error = message });
                }
            }
        );
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        if (options.Mode is "gallery" or "rehearsal")
        {
            var gallery = new Gallery(options.Gallery);
            app.MapGet("/api/gallery", () => gallery.List());
            app.MapGet("/api/gallery/{slug}/capture.jpg", (string slug) => Image(gallery.Image(slug)));
        }
        if (options.Mode == "gallery")
        {
            app.MapGet(
                "/api/session",
                () =>
                    new
                    {
                        mode = "gallery",
                        authenticated = false,
                        csrf = (string?)null,
                    }
            );
            return app;
        }
        var sessions = new OwnerSessions(options, passphrase);
        var worker = new WebWorker(options);
        app.Lifetime.ApplicationStopped.Register(worker.Dispose);
        app.MapGet("/api/session", (HttpContext ctx) => sessions.State(ctx));
        // Authentication precedes paid endpoint input binding. Every mutation including login must prove CSRF.
        app.Use(
            async (ctx, next) =>
            {
                var p = ctx.Request.Path;
                if (
                    p.StartsWithSegments("/api/jobs")
                    || p.StartsWithSegments("/api/draft")
                    || p.StartsWithSegments("/api/homepage-packet")
                    || p.StartsWithSegments("/api/approve")
                    || p.StartsWithSegments("/api/logout")
                )
                    sessions.Require(ctx);
                if (ctx.Request.Method == "POST" && p.StartsWithSegments("/api"))
                {
                    if (!sessions.Csrf(ctx))
                        throw new WorkspaceException("CSRF check failed.", 403);
                    if (!ctx.Request.HasJsonContentType())
                        throw new WorkspaceException("Send a JSON body.", 415);
                }
                await next();
            }
        );
        app.UseRateLimiter();
        app.MapPost("/api/login", (HttpContext ctx, LoginBody b) => sessions.SignIn(ctx, b.Passphrase))
            .RequireRateLimiting("login");
        app.MapPost("/api/logout", (HttpContext ctx) => sessions.SignOut(ctx));
        app.MapPost("/api/draft", (HttpContext ctx, DraftBody b) => worker.Draft(b.Goal, b.Site, sessions.Require(ctx)))
            .RequireRateLimiting("paid");
        app.MapPost(
                "/api/homepage-packet",
                (HttpContext ctx, DraftBody b) => worker.Draft(b.Goal, b.Site, sessions.Require(ctx), homepage: true)
            )
            .RequireRateLimiting("paid");
        app.MapPost(
                "/api/approve",
                (HttpContext ctx, ApproveBody b) => worker.Approve(b.JobId ?? "", b.Digest ?? "", sessions.Require(ctx))
            )
            .RequireRateLimiting("paid");
        app.MapGet("/api/jobs", (HttpContext ctx) => worker.List(sessions.Require(ctx)));
        app.MapGet("/api/jobs/{id}", (HttpContext ctx, string id) => worker.Result(id, sessions.Require(ctx)));
        app.MapGet(
            "/api/jobs/{id}/captures/{step:int}",
            (HttpContext ctx, string id, int step) =>
            {
                sessions.Require(ctx);
                return Image(worker.Screenshot(id, step));
            }
        );
        return app;
    }

    private static IResult Image(byte[]? bytes) =>
        bytes is null ? Results.NotFound(new { error = "Unknown capture." }) : Results.File(bytes, "image/jpeg");

    // Kestrel listens on loopback only, so every connection comes from this machine. Behind the reverse proxy,
    // which overwrites X-Forwarded-For with the TLS client's address, that header names the client. IPv6
    // clients are grouped by /64, since one host can rotate through a whole prefix.
    private static string LoginClient(HttpContext ctx)
    {
        var remote = ctx.Connection.RemoteIpAddress;
        if (
            (remote is null || IPAddress.IsLoopback(remote))
            && ctx.Request.Headers["X-Forwarded-For"] is { Count: 1 } forwarded
            && IPAddress.TryParse(forwarded[0], out var client)
        )
            remote = client;
        if (remote is null)
            return "unknown";
        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();
        if (remote.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var bytes = remote.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            remote = new IPAddress(bytes);
        }
        return remote.ToString();
    }

    private sealed record LoginBody(string? Passphrase);

    private sealed record DraftBody(string? Goal, string? Site);

    private sealed record ApproveBody(string? JobId, string? Digest);
}
