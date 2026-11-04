using System.Net;
using System.Text;
using System.Text.Json;
using HireZero.Agent;
using HireZero.Core;
using HireZero.Integrations.Inference;

public sealed class BrowserTaskTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hz-task-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Site = "https://paper.example.invalid";

    // Answers each model call in order with the given assistant text.
    private sealed class Model(params string[] replies) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(token));
            var text = replies[Math.Min(Bodies.Count - 1, replies.Length - 1)];
            var body = JsonSerializer.Serialize(
                new
                {
                    id = "cmpl-fictional",
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = text }, finish_reason = "stop" },
                    },
                    usage = new { prompt_tokens = 50, completion_tokens = 20 },
                }
            );
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Browser(bool releasedByRun = false) : IBrowserExecutor
    {
        public List<string> Log { get; } = [];
        public string? RunsFolder;
        public bool SavedBeforeRelease;
        public string Identity => "fictional-browser";

        public Task<BrowserExecution> ExecuteAsync(
            BrowserRequest request,
            Func<bool> authorized,
            CancellationToken token
        )
        {
            Log.Add("execute");
            var capture = new BrowserCapture(
                2,
                Site + "/",
                DateTimeOffset.UtcNow,
                "Paper shop",
                "We sell fictional paper.",
                false,
                []
            );
            var result = new BrowserResult(
                request.JobId,
                "completed",
                request.Steps.Length,
                false,
                [capture],
                [],
                [],
                true,
                new(2, 100),
                900
            );
            return Task.FromResult(
                new BrowserExecution(
                    "completed",
                    result,
                    new(2, 100, 0),
                    900,
                    new(releasedByRun, releasedByRun ? "released" : "retained", "browser-" + request.JobId),
                    CreationConfirmed: true,
                    BrowserExited: true
                )
            );
        }

        public Task<BrowserExecution> RecoverAsync(BrowserRequest request, CancellationToken token) =>
            throw new InvalidOperationException("not used");

        public Task<SandboxRelease> ReleaseAsync(
            BrowserRequest request,
            string resourceId,
            bool creationConfirmed,
            CancellationToken token
        )
        {
            Log.Add("release");
            SavedBeforeRelease =
                Directory.GetFiles(RunsFolder!, "execution.json", SearchOption.AllDirectories).Length == 1;
            return Task.FromResult(new SandboxRelease(true, "released", resourceId));
        }
    }

    private (BrowserTask Task, Model Model, Browser Browser) Make(Browser? browser = null, params string[] replies)
    {
        var settings = InferenceSettings.FromEnvironment(n =>
            n == InferenceSettings.VultrKeyVariable ? "fictional-key" : null
        );
        var model = new Model(replies);
        var budget = new InferenceBudget(
            1_000_000,
            10_000_000,
            new InferenceUsageLog(Path.Combine(_folder, "usage.jsonl"))
        );
        browser ??= new Browser();
        browser.RunsFolder = Path.Combine(_folder, "runs");
        return (
            new BrowserTask(new InferenceClient(settings, budget, model), browser, Path.Combine(_folder, "runs")),
            model,
            browser
        );
    }

    // A reply wrapped in a code fence, as models often send it.
    private const string Draft =
        "```json\n"
        + """{"steps":[{"action":"navigate","value":"https://paper.example.invalid/products"},{"action":"read","value":"main"}]}"""
        + "\n```";

    [Fact]
    public async Task TheModelsDraftIsUsedWhenItStaysInsideTheApprovedSites()
    {
        var (task, model, _) = Make(null, Draft);

        var itinerary = await task.DraftAsync("What do they sell?", [Site], Token);

        Assert.Equal("model", itinerary.Source);
        Assert.Equal(["navigate", "read"], itinerary.Request.Steps.Select(s => s.Action));
        Assert.Equal([Site], itinerary.Request.Origins);
        Assert.Contains("approvedSites", Assert.Single(model.Bodies));
    }

    [Fact]
    public async Task ADraftThatLeavesTheApprovedSitesFallsBackToTheTemplateWithoutAskingAgain()
    {
        var offSite =
            """{"steps":[{"action":"navigate","value":"https://collector.example.invalid/"},{"action":"read","value":"body"}]}""";
        var (task, model, _) = Make(null, offSite);

        var itinerary = await task.DraftAsync("What do they sell?", [Site], Token);

        Assert.Equal("template", itinerary.Source);
        Assert.Equal(Site + "/", itinerary.Request.Steps[0].Value);
        Assert.Contains("leaves the approved websites", itinerary.Note);
        Assert.Single(model.Bodies);
    }

    [Fact]
    public async Task AnApprovalForADifferentPacketRunsNothing()
    {
        var (task, _, browser) = Make(null, Draft);
        var itinerary = await task.DraftAsync("goal", [Site], Token);

        await Assert.ThrowsAsync<WorkspaceException>(() =>
            task.RunAsync(itinerary, new string('0', 64), "goal", Token)
        );

        Assert.Empty(browser.Log);
    }

    [Fact]
    public async Task TheResultIsSavedThenTheSandboxIsTornDownThenTheGoalIsAnswered()
    {
        var (task, model, browser) = Make(null, Draft, "They sell fictional paper (Paper shop).");
        var itinerary = await task.DraftAsync("What do they sell?", [Site], Token);

        var outcome = await task.RunAsync(itinerary, itinerary.Digest, "What do they sell?", Token);

        Assert.Equal(["execute", "release"], browser.Log);
        Assert.True(outcome.Release.Confirmed);
        Assert.True(browser.SavedBeforeRelease);
        Assert.True(File.Exists(Path.Combine(outcome.RunFolder, "release.json")));
        Assert.Equal("They sell fictional paper (Paper shop).", outcome.Answer);
        // The page text reaches the model only as data in the answer call.
        Assert.Contains("We sell fictional paper.", model.Bodies[1]);
        Assert.Contains("untrusted website content", model.Bodies[1]);
    }

    [Fact]
    public async Task ARunThatAlreadyReleasedItsSandboxIsNotReleasedAgain()
    {
        var (task, _, browser) = Make(new Browser(releasedByRun: true), Draft, "answer");
        var itinerary = await task.DraftAsync("goal", [Site], Token);

        var outcome = await task.RunAsync(itinerary, itinerary.Digest, "goal", Token);

        Assert.Equal(["execute"], browser.Log);
        Assert.True(outcome.Release.Confirmed);
    }

    [Fact]
    public async Task AnApprovedPacketNeverRunsTwice()
    {
        var (task, _, browser) = Make(null, Draft, "answer");
        var itinerary = await task.DraftAsync("goal", [Site], Token);
        await task.RunAsync(itinerary, itinerary.Digest, "goal", Token);

        await Assert.ThrowsAsync<WorkspaceException>(() => task.RunAsync(itinerary, itinerary.Digest, "goal", Token));

        Assert.Single(browser.Log, "execute");
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
