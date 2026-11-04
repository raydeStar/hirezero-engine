using HireZero.Integrations.Inference;

// The competition's containment rules, checked against the exact docker commands the browser runtime issues.
public sealed partial class DockerBrowserTests
{
    private static string Flag(string[] args, string name) => args[Array.IndexOf(args, name) + 1];

    [Fact]
    public async Task NoHostCredentialReachesTheBrowserSandbox()
    {
        const string FakeKey = "fictional-vultr-key-must-not-leak";
        var previous = Environment.GetEnvironmentVariable(InferenceSettings.VultrKeyVariable);
        Environment.SetEnvironmentVariable(InferenceSettings.VultrKeyVariable, FakeKey);
        try
        {
            var request = BrowserProtocolTests.Scope();
            var engine = new Engine(request);
            var result = await engine.Runtime().ExecuteAsync(request, () => true, Token);

            Assert.Equal("completed", result.State);
            var args = engine.Calls.SelectMany(c => c).ToArray();
            // No environment, env files, mounts or volumes are passed, so nothing from the host can be inherited.
            Assert.DoesNotContain(
                args,
                a => a is "-e" or "--env" or "--env-file" or "-v" or "--volume" or "--mount" or "--volumes-from"
            );
            Assert.DoesNotContain(args, a => a.Contains(FakeKey, StringComparison.Ordinal));
            Assert.DoesNotContain(FakeKey, engine.Input!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InferenceSettings.VultrKeyVariable, previous);
        }
    }

    [Fact]
    public async Task EveryBrowserTaskIsCappedIsolatedAndTornDown()
    {
        var request = BrowserProtocolTests.Scope();
        var engine = new Engine(request);
        var runtime = engine.Runtime();

        var result = await runtime.ExecuteAsync(request, () => true, Token);
        var create = Assert.Single(engine.Calls, c => c.Length > 1 && c[1] == "create");

        Assert.Equal("runsc", Flag(create, "--runtime"));
        Assert.Equal("none", Flag(create, "--network"));
        Assert.Equal("512m", Flag(create, "--memory"));
        Assert.Equal("512m", Flag(create, "--memory-swap"));
        Assert.Equal("1", Flag(create, "--cpus"));
        Assert.Equal("128", Flag(create, "--pids-limit"));
        Assert.Equal("ALL", Flag(create, "--cap-drop"));
        Assert.Contains("--read-only", create);
        // The time cap is part of the approved packet and cannot exceed two minutes.
        Assert.InRange(request.Limits.Seconds, 1, 120);

        Assert.True((await Release(runtime, request, result)).Confirmed);
        Assert.Contains(engine.Calls, c => c.Length > 1 && c[1] == "rm");
        Assert.Null(engine.Container);
    }
}
