using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HireZero.Engine.Web;
using Microsoft.AspNetCore.Builder;

public sealed class WebAppTests : IDisposable
{
    private const string Secret = "fictional-rehearsal-owner";
    private readonly string scratch = Path.Combine(Path.GetTempPath(), "hz-web-test-" + Guid.NewGuid().ToString("N"));
    private string Data => Path.Combine(scratch, "private");
    private string Gallery => Path.Combine(scratch, "selected");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Server(WebApplication App, HttpClient Client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }

    private async Task<Server> Start(string mode = "rehearsal")
    {
        var options = new WebOptions(mode, 0, mode == "gallery" ? null : Data, Gallery, null);
        var app = HireZero.Engine.Web.WebHost.Build(options, mode == "gallery" ? null : Secret);
        await app.StartAsync(Token);
        return new(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) });
    }

    private static async Task<JsonElement> Read(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, $"{r.StatusCode}: {await r.Content.ReadAsStringAsync(Token)}");
        using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync(Token));
        return d.RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> Post(
        HttpClient client,
        string name,
        object value,
        string? csrf = null
    )
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/" + name) { Content = JsonContent.Create(value) };
        if (csrf is not null)
            req.Headers.Add("X-HireZero-CSRF", csrf);
        return await client.SendAsync(req, Token);
    }

    private static async Task<string> Login(HttpClient client)
    {
        var session = await Read(await client.GetAsync("/api/session", Token));
        var signedIn = await Read(
            await Post(client, "login", new { passphrase = Secret }, session.GetProperty("csrf").GetString())
        );
        Assert.True(signedIn.GetProperty("authenticated").GetBoolean());
        return signedIn.GetProperty("csrf").GetString()!;
    }

    private static readonly object Draft = new
    {
        goal = "List the fictional catalogue products.",
        site = "https://catalogue.example.invalid",
    };

    [Fact]
    public async Task AnonymousAndCrossOriginPostsCannotDraftOrApprove()
    {
        await using var server = await Start();
        var c = server.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "draft", Draft)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/jobs", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c, "login", new { passphrase = Secret })).StatusCode);
        var csrf = await Login(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c, "draft", Draft)).StatusCode);
        c.DefaultRequestHeaders.Add("Origin", "https://unrelated.example.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c, "draft", Draft, csrf)).StatusCode);
        c.DefaultRequestHeaders.Remove("Origin");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await Post(c, "approve", new { jobId = "1", digest = "x" })).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await Post(c, "draft", new { goal = "Read", site = "https://127.0.0.1" }, csrf)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            (
                await Post(
                    c,
                    "draft",
                    new { goal = new string('x', 12_000), site = "https://catalogue.example.invalid" },
                    csrf
                )
            ).StatusCode
        );
        Assert.False(File.Exists(Path.Combine(Data, "inference-usage.jsonl")));
    }

    [Fact]
    public async Task ApprovalsAreSessionBoundAndConsumedBeforeDispatch()
    {
        await using var server = await Start();
        var c = server.Client;
        var csrf = await Login(c);
        var stale = await Read(await Post(c, "draft", Draft, csrf));
        var old = new
        {
            jobId = stale.GetProperty("jobId").GetString(),
            digest = stale.GetProperty("digest").GetString(),
        };
        csrf = await Login(c);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(c, "approve", old, csrf)).StatusCode);
        var d = await Read(await Post(c, "draft", Draft, csrf));
        var id = d.GetProperty("jobId").GetString();
        var approval = new { jobId = id, digest = d.GetProperty("digest").GetString() };
        var pending = await Read(await c.GetAsync("/api/jobs/" + id, Token));
        Assert.True(pending.GetProperty("canApprove").GetBoolean());
        Assert.Equal(d.GetProperty("request").GetRawText(), pending.GetProperty("request").GetRawText());
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await Post(c, "approve", new { jobId = id, digest = "changed" }, csrf)).StatusCode
        );
        var attempts = await Task.WhenAll(Post(c, "approve", approval, csrf), Post(c, "approve", approval, csrf));
        Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.Conflict);
        var final = await Read(Assert.Single(attempts, r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal("completed", final.GetProperty("state").GetString());
        Assert.True(final.GetProperty("simulated").GetBoolean());
        Assert.True(final.GetProperty("teardown").GetProperty("confirmed").GetBoolean());
        Assert.Contains("Simulated", final.GetProperty("answer").GetString());
        Assert.Equal(216, final.GetProperty("tokens").GetInt64());
        Assert.Equal(108, final.GetProperty("draftTokens").GetInt64());
        Assert.Equal(108, final.GetProperty("answerTokens").GetInt64());
        Assert.Equal("rehearsal", final.GetProperty("provider").GetString());
        Assert.Equal("simulated", final.GetProperty("model").GetString());
        Assert.Equal(
            "https://assets.example.invalid",
            Assert.Single(final.GetProperty("blockedOrigins").EnumerateArray()).GetString()
        );
        var shot = Assert
            .Single(final.GetProperty("captures").EnumerateArray())
            .GetProperty("screenshotUrl")
            .GetString();
        var image = await c.GetAsync(shot, Token);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);
        Assert.InRange((await image.Content.ReadAsByteArrayAsync(Token)).Length, 5, 256 * 1024);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/jobs/{id}/captures/99", Token)).StatusCode);
        await Read(await Post(c, "logout", new { }, csrf));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(shot, Token)).StatusCode);
    }

    [Fact]
    public async Task GalleryOnlyServesBoundedExplicitlySelectedFiles()
    {
        var selected = Directory.CreateDirectory(Path.Combine(Gallery, "catalogue")).FullName;
        var summary = new
        {
            title = "Fictional gallery",
            goal = "Read",
            site = "https://catalogue.example.invalid",
            answer = "Paper",
            source = "simulated",
            state = "completed",
            teardown = "simulated-released",
            limits = new { seconds = 30 },
            blockedOrigins = Array.Empty<string>(),
        };
        File.WriteAllText(Path.Combine(selected, "summary.json"), JsonSerializer.Serialize(summary));
        File.WriteAllText(Path.Combine(selected, "execution.json"), "private");
        var unknown = Directory.CreateDirectory(Path.Combine(Gallery, "raw")).FullName;
        File.WriteAllText(Path.Combine(unknown, "summary.json"), """{"privateKey":"fictional-value"}""");
        File.WriteAllText(Path.Combine(unknown, "capture.jpg"), "pretend image");
        await using var server = await Start("gallery");
        var c = server.Client;
        var item = Assert.Single((await Read(await c.GetAsync("/api/gallery", Token))).EnumerateArray());
        Assert.Equal("catalogue", item.GetProperty("slug").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("screenshotUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("limits").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("tokens").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("wallMilliseconds").ValueKind);
        using (
            var fixture = typeof(HireZero.Engine.Web.WebHost).Assembly.GetManifestResourceStream(
                "HireZero.Engine.Web.Rehearsal.capture.jpg"
            )!
        )
        using (var file = File.Create(Path.Combine(selected, "capture.jpg")))
            await fixture.CopyToAsync(file, Token);
        var photo = await c.GetAsync("/api/gallery/catalogue/capture.jpg", Token);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await c.GetAsync("/api/gallery/catalogue/execution.json", Token)).StatusCode
        );
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/gallery/raw/capture.jpg", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/jobs", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c, "draft", Draft)).StatusCode);
        Assert.False(Directory.Exists(Data));
    }

    [Fact]
    public async Task RestartExposesInterruptedEvidenceAndRejectsFreshPaidWorkUntilReview()
    {
        string id;
        await using (var server = await Start())
        {
            var csrf = await Login(server.Client);
            var draft = await Read(await Post(server.Client, "draft", Draft, csrf));
            id = draft.GetProperty("jobId").GetString()!;
        }
        File.WriteAllText(
            Path.Combine(Data, "active-operation.json"),
            JsonSerializer.Serialize(new { operation = "approve", jobId = id })
        );
        File.WriteAllText(Path.Combine(Data, "jobs", id, "consumed.json"), "{}");
        var legacyPath = Path.Combine(Data, "jobs", id, "draft.json");
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(legacyPath))!.AsObject();
        legacy.Remove("provider");
        legacy.Remove("model");
        File.WriteAllText(legacyPath, legacy.ToJsonString());
        Directory.CreateDirectory(Path.Combine(Data, "network"));
        File.WriteAllText(Path.Combine(Data, "network", id + ".jsonl"), "{");
        await using var restarted = await Start();
        var csrf2 = await Login(restarted.Client);
        var item = await Read(await restarted.Client.GetAsync("/api/jobs/" + id, Token));
        Assert.Equal("interrupted", item.GetProperty("state").GetString());
        Assert.False(item.GetProperty("canApprove").GetBoolean());
        Assert.False(item.GetProperty("teardown").GetProperty("confirmed").GetBoolean());
        Assert.False(item.GetProperty("eventsComplete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("provider").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("model").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("answerTokens").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(restarted.Client, "draft", Draft, csrf2)).StatusCode);
        Assert.Single(File.ReadAllLines(Path.Combine(Data, "inference-usage.jsonl")));
    }

    [Fact]
    public async Task LoginAndPaidEndpointsHaveIndependentLimits()
    {
        await using var server = await Start();
        var c = server.Client;
        var csrf = await Login(c);
        for (var i = 0; i < 4; i++)
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await Post(c, "login", new { passphrase = "wrong" }, csrf)).StatusCode
            );
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await Post(c, "login", new { passphrase = Secret }, csrf)).StatusCode
        );
        for (var i = 0; i < 12; i++)
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await Post(c, "approve", new { jobId = "none", digest = "none" }, csrf)).StatusCode
            );
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(c, "draft", Draft, csrf)).StatusCode);
    }

    [Fact]
    public async Task WorkerRequiresItsProxyHostAndHttpsBeforeIssuingAnOwnerCookie()
    {
        var origin = new Uri("https://localhost:8443");
        var options = new WebOptions("worker", 0, Data, null, origin);
        var app = HireZero.Engine.Web.WebHost.Build(options, Secret);
        await app.StartAsync(Token);
        await using var server = new Server(app, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) });
        var c = server.Client;
        c.DefaultRequestHeaders.Host = origin.Authority;
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/session", Token)).StatusCode);
        c.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        c.DefaultRequestHeaders.Host = "unrelated.example.invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/session", Token)).StatusCode);
        c.DefaultRequestHeaders.Host = origin.Authority;
        var response = await c.GetAsync("/api/session", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("__Host-HireZeroOwner=", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(Data, "inference-usage.jsonl")));
    }

    [Fact]
    public async Task ChangingTheSavedPacketCannotAuthorizeTheModifiedInstructions()
    {
        await using var server = await Start();
        var c = server.Client;
        var csrf = await Login(c);
        var draft = await Read(await Post(c, "homepage-packet", Draft, csrf));
        var id = draft.GetProperty("jobId").GetString()!;
        var path = Path.Combine(Data, "jobs", id, "draft.json");
        var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var request = saved["packet"]!["request"]!;
        request["steps"]![1]!["value"] = "main";
        File.WriteAllText(path, saved.ToJsonString());
        var forged = CompanyJson.Digest(CompanyJson.Decode<BrowserRequest>(request.ToJsonString()));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await Post(c, "approve", new { jobId = id, digest = forged }, csrf)).StatusCode
        );
        Assert.False(Directory.Exists(Path.Combine(Data, "runs", id)));
        Assert.False(File.Exists(Path.Combine(Data, "inference-usage.jsonl")));
    }

    [Fact]
    public async Task OwnerHomepagePacketStartsAFreshRunAndSpendsNoDraftTokens()
    {
        await using var server = await Start();
        var c = server.Client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "homepage-packet", Draft)).StatusCode);
        var csrf = await Login(c);
        var old = await Read(await Post(c, "draft", Draft, csrf));
        var oldId = old.GetProperty("jobId").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c, "homepage-packet", Draft)).StatusCode);
        var d = await Read(await Post(c, "homepage-packet", Draft, csrf));
        var id = d.GetProperty("jobId").GetString();
        var digest = d.GetProperty("digest").GetString();
        Assert.NotEqual(oldId, id);
        Assert.NotEqual(old.GetProperty("digest").GetString(), digest);
        Assert.Equal("owner-template", d.GetProperty("source").GetString());
        var steps = d.GetProperty("request").GetProperty("steps");
        Assert.Equal("https://catalogue.example.invalid/", steps[0].GetProperty("value").GetString());
        Assert.Equal("body", steps[1].GetProperty("value").GetString());
        var previous = await Read(await c.GetAsync("/api/jobs/" + oldId, Token));
        Assert.Equal("expired", previous.GetProperty("state").GetString());
        Assert.Equal(108, previous.GetProperty("tokens").GetInt64());
        var pending = await Read(await c.GetAsync("/api/jobs/" + id, Token));
        Assert.Equal(0, pending.GetProperty("tokens").GetInt64());
        Assert.Single(File.ReadAllLines(Path.Combine(Data, "inference-usage.jsonl")));
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await Post(c, "approve", new { jobId = oldId, digest = old.GetProperty("digest").GetString() }, csrf)
            ).StatusCode
        );
        var final = await Read(await Post(c, "approve", new { jobId = id, digest }, csrf));
        Assert.Equal(108, final.GetProperty("tokens").GetInt64());
        Assert.Equal(0, final.GetProperty("draftTokens").GetInt64());
        Assert.Equal(108, final.GetProperty("answerTokens").GetInt64());
        Assert.Equal(2, File.ReadAllLines(Path.Combine(Data, "inference-usage.jsonl")).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(scratch))
            Directory.Delete(scratch, true);
    }
}
