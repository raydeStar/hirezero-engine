using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireZero.Agent;
using HireZero.Core;
using HireZero.Integrations.Inference;
using HireZero.Integrations.Sandbox;
using HireZero.Integrations.Web;

namespace HireZero.Engine.Web;

internal sealed record WebRuntime(InferenceClient Model, BrowserTask Task, string Identity) : IDisposable
{
    public void Dispose() => Model.Dispose();

    public static async Task<WebRuntime> Create(WebOptions options, InferenceUsageLog usage)
    {
        var root = options.Data!;
        var rehearsal = options.Mode == "rehearsal";
        var settings = rehearsal
            ? new InferenceSettings(
                "rehearsal",
                new Uri("https://rehearsal.invalid/"),
                "simulated",
                "fixture",
                8000,
                TimeSpan.FromSeconds(5),
                "low"
            )
            : InferenceSettings.FromEnvironment();
        if (!rehearsal && settings.LocalTestOnly && options.Origin?.IsLoopback != true)
            throw new WorkspaceException(
                "The stand-in provider is restricted to private HTTPS loopback rehearsal.",
                503
            );
        var budget = InferenceBudget.FromEnvironment(usage, rehearsal ? _ => null : null);
        BrowserNetworkLog Log(string id) => new(Path.Combine(root, "network", id + ".jsonl"));
        IBrowserExecutor browser;
        if (rehearsal)
            browser = new RehearsalBrowser(Log);
        else
        {
            const string endpoint = "unix:///var/run/docker.sock";
            var path =
                Environment.GetEnvironmentVariable("HIREZERO_BROWSER_RECEIPT")
                ?? "/opt/hirezero-qualification/receipts/browser-qualification.json";
            var docker = Environment.GetEnvironmentVariable("HIREZERO_BROWSER_DOCKER") ?? "/usr/bin/docker";
            var receipt = BrowserQualification.Read(path);
            if (receipt is null || !receipt.BoundTo(receipt.Image, receipt.InstallationId, endpoint))
                throw new WorkspaceException(
                    "A browser qualification receipt for the local Linux engine is required.",
                    503
                );
            var qualified = new QualifiedDockerBrowser(
                new DockerCommands(docker, endpoint),
                receipt,
                () => BrowserQualification.Read(path),
                () => SandboxQualification.ExecutableHash(docker),
                true,
                (request, authorized) =>
                    new PublicBrowserNetwork(request, authorized, observe: Log(request.JobId).Append)
            );
            await qualified.RefreshAsync(CancellationToken.None);
            if (!qualified.Availability.Available)
                throw new WorkspaceException("The browser runtime is not qualified and ready.", 503);
            browser = qualified;
        }
        var model = new InferenceClient(settings, budget, rehearsal ? new RehearsalModel() : null);
        return new(model, new BrowserTask(model, browser, Path.Combine(root, "runs")), browser.Identity);
    }
}

internal sealed class RehearsalModel : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
        using var data = JsonDocument.Parse(
            body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!
        );
        var reply = data.RootElement.TryGetProperty("approvedSites", out var origins)
            ? CompanyJson.Encode(
                new { steps = new BrowserStep[] { new("navigate", origins[0].GetString() + "/"), new("read", "body") } }
            )
            : "Simulated answer: the fictional catalogue lists paper, envelopes and sketchbooks. No live website was visited.";
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    id = "rehearsal",
                    choices = new[] { new { message = new { content = reply }, finish_reason = "stop" } },
                    usage = new { prompt_tokens = 80, completion_tokens = 28 },
                }
            ),
        };
    }
}

internal sealed class RehearsalBrowser(Func<string, BrowserNetworkLog> log) : IBrowserExecutor
{
    public string Identity => "simulated-offline-browser";

    public Task<BrowserExecution> ExecuteAsync(BrowserRequest request, Func<bool> authorized, CancellationToken token)
    {
        using var file = typeof(RehearsalBrowser).Assembly.GetManifestResourceStream(
            "HireZero.Engine.Web.Rehearsal.capture.jpg"
        )!;
        using var image = new MemoryStream();
        file.CopyTo(image);
        var site = request.Origins[0] + "/";
        const string offsite = "https://assets.example.invalid";
        log(request.JobId)
            .Append(
                new(
                    DateTimeOffset.UtcNow,
                    request.JobId,
                    "allowed",
                    "page",
                    "GET",
                    site,
                    200,
                    "Simulated broker observation"
                )
            );
        var capture = new BrowserCapture(
            2,
            site,
            DateTimeOffset.UtcNow,
            "Simulated paper catalogue",
            "The fictional shop stocks paper, envelopes and sketchbooks.",
            false,
            [],
            new("image/jpeg", Convert.ToBase64String(image.ToArray()))
        );
        var result = new BrowserResult(
            request.JobId,
            "completed",
            2,
            true,
            [capture],
            [new(offsite + "/font", "This request leaves the approved browser websites.")],
            [],
            true,
            new(2, 100),
            40
        );
        result.Validate(request);
        return Task.FromResult(
            new BrowserExecution(
                "completed",
                result,
                new(1, 100, 0),
                40,
                new(false, "simulated-awaiting-release", "simulated"),
                CreationConfirmed: true,
                BrowserExited: true
            )
        );
    }

    public Task<BrowserExecution> RecoverAsync(BrowserRequest request, CancellationToken token) =>
        throw new NotSupportedException();

    public Task<SandboxRelease> ReleaseAsync(
        BrowserRequest request,
        string resourceId,
        bool creationConfirmed,
        CancellationToken token
    ) => Task.FromResult(new SandboxRelease(true, "simulated-released", resourceId));
}
