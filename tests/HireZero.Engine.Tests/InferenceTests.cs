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
        using var client = new InferenceClient(Vultr(), log, handler);

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
        using var client = new InferenceClient(Vultr((InferenceSettings.MaxOutputVariable, "500")), handler: handler);

        await client.CompleteAsync("s", "u", "test", 90_000, TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(handler.Calls[0].Body);
        Assert.Equal(500, json.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task ARefusalIsReportedOnceAndNeverRetried()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, """{"error":{"type":"invalid_api_key"}}""");
        using var client = new InferenceClient(Vultr(), handler: handler);

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
        using var client = new InferenceClient(Vultr(), handler: handler);

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
        using var client = new InferenceClient(Vultr(), log, handler);

        var error = await Assert.ThrowsAsync<InferenceUncertainException>(() =>
            client.CompleteAsync("s", "u", "test", token: TestContext.Current.CancellationToken)
        );

        Assert.Equal(140, error.Usage!.OutputTokens);
        Assert.Equal("withheld", Assert.Single(log.Read()).Outcome);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
