using System.Text.Json;
using HireZero.Agent;
using HireZero.Core;
using HireZero.Integrations.Inference;

public sealed class ScriptTaskTests : IDisposable
{
    private const string Script = "console.log(2 + 3)";
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "hz-script-task-" + Guid.NewGuid().ToString("N")
    );
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private InferenceClient Model(FakeCompletionHandler handler) =>
        new(
            InferenceSettings.FromEnvironment(name =>
                name == InferenceSettings.VultrKeyVariable ? "fictional-key" : null
            ),
            InferenceBudget.FromEnvironment(new InferenceUsageLog(Path.Combine(_folder, "usage.jsonl")), _ => null),
            handler
        );

    private sealed class Sandbox(string runs) : ISandboxExecutor
    {
        public string Identity => "fictional-sandbox";
        public int Dispatches;
        public int Releases;
        public Action<SandboxRequest>? OnExecute;
        public SandboxResult Result = new(
            "completed",
            0,
            "5",
            "",
            [],
            30,
            new(false, "awaiting-release", "fictional-resource"),
            CreationConfirmed: true
        );
        public SandboxRelease Release = new(true, "released", "fictional-resource");

        public Task<SandboxResult> ExecuteAsync(SandboxRequest packet, Func<bool> authorized, CancellationToken token)
        {
            Assert.True(authorized());
            Interlocked.Increment(ref Dispatches);
            using var saved = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(runs, "scripts", packet.JobId, "packet.json"))
            );
            Assert.Equal(Identity, saved.RootElement.GetProperty("runtime").GetString());
            Assert.Equal(CompanyJson.Digest(packet), saved.RootElement.GetProperty("approvedDigest").GetString());
            Assert.Equal(CompanyJson.Encode(packet), saved.RootElement.GetProperty("request").GetRawText());
            OnExecute?.Invoke(packet);
            return Task.FromResult(Result);
        }

        public Task<SandboxResult> RecoverAsync(SandboxRequest packet, CancellationToken token) =>
            throw new InvalidOperationException("Recovery is a separate operator action.");

        public Task<SandboxRelease> ReleaseAsync(string companyId, string jobId, bool created, CancellationToken token)
        {
            Interlocked.Increment(ref Releases);
            Assert.Equal("demo", companyId);
            Assert.Equal(Result.CreationConfirmed, created);
            var saved = File.ReadAllText(Path.Combine(runs, "scripts", jobId, "result.json"));
            Assert.Equal(CompanyJson.Encode(Result), saved);
            return Task.FromResult(Release);
        }
    }

    [Fact]
    public async Task OnlyTheExactReviewedScriptPacketCanRun()
    {
        var handler = new FakeCompletionHandler(CompanyJson.Encode(new { script = Script }));
        using var model = Model(handler);
        var sandbox = new Sandbox(_folder);
        var task = new ScriptTask(model, sandbox, _folder);
        var packet = await task.DraftAsync("Add two to three", Token);
        Assert.Single(handler.Bodies);
        Assert.Equal(Script, packet.Script);
        Assert.Empty(packet.Inputs);
        Assert.Empty(packet.Outputs);
        Assert.Null(packet.Tier);
        Assert.Equal(new SandboxLimits(), packet.Limits);
        var digest = CompanyJson.Digest(packet);

        var changed = packet with { Script = "console.log(99)" };
        var refusal = await Assert.ThrowsAsync<WorkspaceException>(() => task.RunAsync(changed, digest, Token));
        Assert.Equal(403, refusal.Status);
        Assert.Equal(0, sandbox.Dispatches);
        Assert.False(Directory.Exists(Path.Combine(_folder, "scripts")));

        var outcome = await task.RunAsync(packet, digest, Token);
        Assert.Equal("5", outcome.Result.Stdout);
        Assert.Equal(1, sandbox.Dispatches);
        Assert.Equal(1, sandbox.Releases);
        Assert.Equal(
            sandbox.Release,
            CompanyJson.Decode<SandboxRelease>(File.ReadAllText(Path.Combine(outcome.RunFolder, "release.json")))
        );
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task AModelCannotChooseJobIdsOrHostOutputPaths()
    {
        var handler = new FakeCompletionHandler(
            CompanyJson.Encode(new { script = Script, jobId = "../outside" }),
            CompanyJson.Encode(new { script = Script, outputs = new[] { "../outside" } })
        );
        using var model = Model(handler);
        var sandbox = new Sandbox(_folder);
        var task = new ScriptTask(model, sandbox, _folder);
        await Assert.ThrowsAsync<JsonException>(() => task.DraftAsync("try a job id", Token));
        await Assert.ThrowsAsync<JsonException>(() => task.DraftAsync("try an output path", Token));
        Assert.Equal(2, handler.Bodies.Count);
        Assert.Equal(0, sandbox.Dispatches);
    }

    [Fact]
    public async Task MutatingTheCallersFileArrayCannotChangeTheDispatchedPacket()
    {
        using var model = Model(new FakeCompletionHandler());
        var draft = new SandboxRequest(
            Guid.NewGuid().ToString("N"),
            "demo",
            Script,
            [new("numbers.txt", "2 3")],
            [],
            new()
        );
        var digest = CompanyJson.Digest(draft);
        var sandbox = new Sandbox(_folder)
        {
            OnExecute = dispatched =>
            {
                draft.Inputs[0] = new("numbers.txt", "99");
                Assert.Equal(digest, CompanyJson.Digest(dispatched));
            },
        };
        await new ScriptTask(model, sandbox, _folder).RunAsync(draft, digest, Token);
    }

    [Fact]
    public async Task AFaultSavingTheResultPreservesTheWorkersLogAndBlocksRedispatch()
    {
        using var model = Model(new FakeCompletionHandler(CompanyJson.Encode(new { script = Script })));
        var sandbox = new Sandbox(_folder)
        {
            OnExecute = p =>
                File.WriteAllText(Path.Combine(_folder, "scripts", p.JobId, "result.json"), "existing record"),
        };
        var task = new ScriptTask(model, sandbox, _folder);
        var packet = await task.DraftAsync("Add two to three", Token);
        var digest = CompanyJson.Digest(packet);
        await Assert.ThrowsAsync<WorkspaceException>(() => task.RunAsync(packet, digest, Token));
        Assert.Equal(
            "existing record",
            File.ReadAllText(Path.Combine(_folder, "scripts", packet.JobId, "result.json"))
        );
        await Assert.ThrowsAsync<WorkspaceException>(() => task.RunAsync(packet, digest, Token));
        Assert.Equal(1, sandbox.Dispatches);
        Assert.Equal(0, sandbox.Releases);
    }

    [Fact]
    public async Task ConcurrentDispatchAndRestartCannotReplayAnUncertainScript()
    {
        using var model = Model(new FakeCompletionHandler(CompanyJson.Encode(new { script = Script })));
        var sandbox = new Sandbox(_folder)
        {
            Result = new("unknown", null, "", "", [], 30, new(false, "awaiting-release", "fictional-resource")),
            Release = new(false, "provisioning-unconfirmed", "fictional-resource"),
        };
        var task = new ScriptTask(model, sandbox, _folder);
        var packet = await task.DraftAsync("Add two to three", Token);
        var digest = CompanyJson.Digest(packet);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable
            .Range(0, 4)
            .Select(async _ =>
            {
                await start.Task;
                try
                {
                    return await new ScriptTask(model, sandbox, _folder).RunAsync(packet, digest, Token);
                }
                catch (WorkspaceException e) when (e.Status == 409)
                {
                    return null;
                }
            })
            .ToArray();
        start.SetResult();
        var outcome = Assert.Single((await Task.WhenAll(attempts)).OfType<ScriptOutcome>());
        Assert.False(outcome.Release.Confirmed);
        Assert.Equal(
            sandbox.Release,
            CompanyJson.Decode<SandboxRelease>(File.ReadAllText(Path.Combine(outcome.RunFolder, "release.json")))
        );
        var restarted = new ScriptTask(model, sandbox, _folder);
        var refusal = await Assert.ThrowsAsync<WorkspaceException>(() => restarted.RunAsync(packet, digest, Token));
        Assert.Equal(409, refusal.Status);
        Assert.Equal(1, sandbox.Dispatches);
        Assert.Equal(1, sandbox.Releases);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
