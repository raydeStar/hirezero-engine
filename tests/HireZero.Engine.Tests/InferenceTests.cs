using System.Net;
using System.Text;
using System.Text.Json;
using HireZero.Integrations.Inference;

namespace HireZero.Engine.Tests;

public sealed class InferenceTests : IDisposable
{
    private const string FakeKey = "fictional-test-key-0000";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hz-inference-" + Guid.NewGuid().ToString("N"));

    private static Func<string, string?> Env(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;

    private static InferenceSettings Vultr(params (string, string)[] extra) =>
        InferenceSettings.FromEnvironment(Env([(InferenceSettings.VultrKeyVariable, FakeKey), .. extra]));

    private InferenceUsageLog Log() => new(Path.Combine(_folder, "usage.jsonl"));

    private InferenceBudget Budget(InferenceUsageLog? log = null, long run = 1_000_000, long month = 10_000_000) =>
        new(run, month, log ?? Log(), () => new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));

    private static InferenceUsageEntry Earlier(DateTimeOffset at, long charged) =>
        new(at, "vultr", "api.vultrinference.com", "m", "earlier", "complete", 1, 1, null, 1, "x", charged);

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token
        )
        {
            Calls.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(token)));
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static string Completion(string finish = "stop") =>
        """{"id":"cmpl-fictional-1","choices":[{"message":{"role":"assistant","content":"pong"},"finish_reason":"""
        + JsonSerializer.Serialize(finish)
        + """}],"usage":{"prompt_tokens":21,"completion_tokens":140,"completion_tokens_details":{"reasoning_tokens":131}}}""";

    [Fact]
    public void VultrAndDeepSeekAreTheDefaults()
    {
        var settings = Vultr();

        Assert.Equal("vultr", settings.Provider);
        Assert.Equal("https://api.vultrinference.com/v1/", settings.BaseUrl.AbsoluteUri);
        Assert.Equal("deepseek-v4.1-flash-1017", settings.Model);
        Assert.Equal(24_000, settings.MaxOutputTokens);
        Assert.False(settings.LocalTestOnly);
    }

    [Fact]
    public void TheBaseUrlAndModelComeFromTheEnvironment()
    {
        var settings = Vultr(
            (InferenceSettings.VultrBaseUrlVariable, "https://inference.example.test/v1/"),
            (InferenceSettings.ModelVariable, "fictional-model")
        );

        Assert.Equal("https://inference.example.test/v1/", settings.BaseUrl.AbsoluteUri);
        Assert.Equal("fictional-model", settings.Model);
    }

    [Fact]
    public void AMissingKeyNamesTheVariableAndTheKeyNeverAppearsInText()
    {
        var error = Assert.Throws<InferenceConfigurationException>(() => InferenceSettings.FromEnvironment(Env()));
        Assert.Contains("VULTR_INFERENCE_API_KEY", error.Message);

        Assert.DoesNotContain(FakeKey, Vultr().ToString());
    }

    [Fact]
    public void OpenRouterIsUsedOnlyWhenNamedAndIsMarkedLocalTestOnly()
    {
        var settings = InferenceSettings.FromEnvironment(
            Env(
                (InferenceSettings.ProviderVariable, "openrouter"),
                (InferenceSettings.OpenRouterKeyVariable, FakeKey),
                (InferenceSettings.VultrKeyVariable, "ignored")
            )
        );

        Assert.Equal("openrouter", settings.Provider);
        Assert.Equal("https://openrouter.ai/api/v1/", settings.BaseUrl.AbsoluteUri);
        Assert.True(settings.LocalTestOnly);
        Assert.Throws<InferenceConfigurationException>(() =>
            InferenceSettings.FromEnvironment(Env((InferenceSettings.ProviderVariable, "openai")))
        );
    }

    [Fact]
    public void APlainHttpBaseUrlIsRefusedUnlessItIsLoopback()
    {
        Assert.Throws<InferenceConfigurationException>(() =>
            Vultr((InferenceSettings.VultrBaseUrlVariable, "http://inference.example.test/v1"))
        );
        Assert.Equal(
            "127.0.0.1",
            Vultr((InferenceSettings.VultrBaseUrlVariable, "http://127.0.0.1:9/v1")).BaseUrl.Host
        );
    }

    [Fact]
    public async Task OneCallGoesToTheConfiguredEndpointAndLogsTokensButNotText()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Completion());
        var log = new InferenceUsageLog(Path.Combine(_folder, "usage.jsonl"));
        using var client = new InferenceClient(Vultr(), Budget(log), handler);

        var reply = await client.CompleteAsync(
            "fictional system text",
            "fictional user text",
            "test",
            2000,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("pong", reply.Text);
        Assert.Equal(new InferenceUsage(21, 140, 131), reply.Usage);
        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal("https://api.vultrinference.com/v1/chat/completions", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("deepseek-v4.1-flash-1017", json.RootElement.GetProperty("model").GetString());
        Assert.Equal(2000, json.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("low", json.RootElement.GetProperty("reasoning_effort").GetString());

        var entry = Assert.Single(log.Read());
        Assert.Equal(
            ("vultr", "api.vultrinference.com", "complete", 21L, 140L, 131L),
            (entry.Provider, entry.Host, entry.Outcome, entry.InputTokens, entry.OutputTokens, entry.ReasoningTokens)
        );
        var written = File.ReadAllText(log.Path);
        Assert.DoesNotContain("fictional user text", written);
        Assert.DoesNotContain("pong", written);
        Assert.DoesNotContain(FakeKey, written);
    }

    [Fact]
    public async Task ACallNeverAsksForMoreOutputThanTheConfiguredCap()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Completion());
        using var client = new InferenceClient(Vultr((InferenceSettings.MaxOutputVariable, "500")), Budget(), handler);

        await client.CompleteAsync("s", "u", "test", 90_000, TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(handler.Calls[0].Body);
        Assert.Equal(500, json.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task ARefusalIsReportedOnceAndNeverRetried()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, """{"error":{"type":"invalid_api_key"}}""");
        using var client = new InferenceClient(Vultr(), Budget(), handler);

        var error = await Assert.ThrowsAsync<InferenceRefusedException>(() =>
            client.CompleteAsync("s", "u", "test", token: TestContext.Current.CancellationToken)
        );

        Assert.Contains("HTTP 401, invalid_api_key", error.Message);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task AServerFailureIsAnUnknownOutcomeAndNeverRetried()
    {
        var handler = new FakeHandler(HttpStatusCode.BadGateway, "");
        using var client = new InferenceClient(Vultr(), Budget(), handler);

        await Assert.ThrowsAsync<InferenceUncertainException>(() =>
            client.CompleteAsync("s", "u", "test", token: TestContext.Current.CancellationToken)
        );

        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task ATruncatedReplyIsWithheldButItsUsageIsLogged()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Completion("length"));
        var log = new InferenceUsageLog(Path.Combine(_folder, "usage.jsonl"));
        using var client = new InferenceClient(Vultr(), Budget(log), handler);

        var error = await Assert.ThrowsAsync<InferenceUncertainException>(() =>
            client.CompleteAsync("s", "u", "test", token: TestContext.Current.CancellationToken)
        );

        Assert.Equal(140, error.Usage!.OutputTokens);
        Assert.Equal("withheld", Assert.Single(log.Read()).Outcome);
    }

    [Fact]
    public async Task ACallThatCouldOverrunTheRunLimitIsRefusedBeforeItIsSent()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Completion());
        var budget = Budget(run: 3_000);
        using var client = new InferenceClient(Vultr(), budget, handler);

        // 1 + 1 input bytes, 256 framing and 2,000 output: 2,258 fits; the provider reports 161, so 2,839 remain.
        await client.CompleteAsync("s", "u", "test", 2000, TestContext.Current.CancellationToken);
        Assert.Equal(161, budget.RunUsed);
        var error = await Assert.ThrowsAsync<InferenceBudgetException>(() =>
            client.CompleteAsync("s", "u", "test", 2900, TestContext.Current.CancellationToken)
        );

        Assert.Contains(InferenceBudget.RunVariable, error.Message);
        Assert.Single(handler.Calls);
        Assert.Single(budget.Log.Read());
    }

    [Fact]
    public async Task TheMonthlyLimitCountsThisMonthsLogAcrossRunsAndIgnoresLastMonth()
    {
        var log = Log();
        log.Append(Earlier(new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero), 9_000));
        log.Append(Earlier(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), 7_000));
        var handler = new FakeHandler(HttpStatusCode.OK, Completion());
        var budget = Budget(log, month: 10_000);
        using var client = new InferenceClient(Vultr(), budget, handler);

        Assert.Equal(7_000, budget.MonthUsed());
        await client.CompleteAsync("s", "u", "test", 2000, TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<InferenceBudgetException>(() =>
            client.CompleteAsync("s", "u", "test", 3000, TestContext.Current.CancellationToken)
        );

        Assert.Contains(InferenceBudget.MonthlyVariable, error.Message);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task AnUnknownOutcomeKeepsItsWholeReservationAndARefusalChargesNothing()
    {
        var unknown = Budget();
        using (var client = new InferenceClient(Vultr(), unknown, new FakeHandler(HttpStatusCode.BadGateway, "")))
            await Assert.ThrowsAsync<InferenceUncertainException>(() =>
                client.CompleteAsync("s", "u", "test", 2000, TestContext.Current.CancellationToken)
            );
        Assert.Equal(2_258, unknown.RunUsed);
        Assert.Equal(2_258, Assert.Single(unknown.Log.Read()).ChargedTokens);

        var refused = Budget(new InferenceUsageLog(Path.Combine(_folder, "refused.jsonl")));
        using (var client = new InferenceClient(Vultr(), refused, new FakeHandler(HttpStatusCode.BadRequest, "{}")))
            await Assert.ThrowsAsync<InferenceRefusedException>(() =>
                client.CompleteAsync("s", "u", "test", 2000, TestContext.Current.CancellationToken)
            );
        Assert.Equal(0, refused.RunUsed);
    }

    [Fact]
    public void TokenLimitsComeFromTheEnvironment()
    {
        var budget = InferenceBudget.FromEnvironment(
            Log(),
            Env((InferenceBudget.RunVariable, "5000"), (InferenceBudget.MonthlyVariable, "60000"))
        );

        Assert.Equal((5_000L, 60_000L), (budget.RunLimit, budget.MonthlyLimit));
        Assert.Equal(200_000, InferenceBudget.FromEnvironment(Log(), Env()).RunLimit);
        Assert.Throws<InferenceConfigurationException>(() =>
            InferenceBudget.FromEnvironment(Log(), Env((InferenceBudget.RunVariable, "0")))
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
