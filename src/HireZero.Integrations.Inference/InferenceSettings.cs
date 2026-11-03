namespace HireZero.Integrations.Inference;

// Which OpenAI-compatible endpoint every model call goes to, read from the environment. Vultr Serverless Inference is
// the default and the only provider meant for deployment; OpenRouter exists solely so the engine can be tested locally
// before a Vultr key exists, and must be chosen by name. The key is held here but never printed or logged.
public sealed record InferenceSettings(
    string Provider,
    Uri BaseUrl,
    string Model,
    string ApiKey,
    int MaxOutputTokens,
    TimeSpan Timeout,
    string ReasoningEffort
)
{
    public const string Vultr = "vultr";
    public const string OpenRouter = "openrouter";

    public const string ProviderVariable = "HIREZERO_INFERENCE_PROVIDER";
    public const string ModelVariable = "HIREZERO_INFERENCE_MODEL";
    public const string MaxOutputVariable = "HIREZERO_INFERENCE_MAX_OUTPUT_TOKENS";
    public const string TimeoutVariable = "HIREZERO_INFERENCE_TIMEOUT_SECONDS";
    public const string ReasoningVariable = "HIREZERO_INFERENCE_REASONING_EFFORT";
    public const string VultrKeyVariable = "VULTR_INFERENCE_API_KEY";
    public const string VultrBaseUrlVariable = "VULTR_INFERENCE_BASE_URL";
    public const string OpenRouterKeyVariable = "OPENROUTER_API_KEY";

    public const string VultrBaseUrl = "https://api.vultrinference.com/v1";
    public const string VultrModel = "deepseek-v4.1-flash-1017";
    public const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    public const string OpenRouterModel = "deepseek/deepseek-v4.1-flash-1017";

    // DeepSeek V4.1 Flash reasons before it answers and Vultr cannot turn that off, so a weekly plan used 8,000 to
    // 15,300 output tokens and up to 290 seconds on a slow host in earlier tests. These defaults leave room for that.
    public const int DefaultMaxOutputTokens = 24_000;
    public const int DefaultTimeoutSeconds = 300;
    public const string DefaultReasoningEffort = "low";

    private static readonly string[] Efforts = ["minimal", "low", "medium", "high"];

    public bool LocalTestOnly => Provider == OpenRouter;

    public static InferenceSettings FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        string? Value(string name) =>
            read(name) is { } value && value.Trim() is { Length: > 0 } trimmed ? trimmed : null;

        var provider = (Value(ProviderVariable) ?? Vultr).ToLowerInvariant();
        var (baseUrl, model, keyVariable) = provider switch
        {
            Vultr => (Value(VultrBaseUrlVariable) ?? VultrBaseUrl, VultrModel, VultrKeyVariable),
            OpenRouter => (OpenRouterBaseUrl, OpenRouterModel, OpenRouterKeyVariable),
            _ => throw new InferenceConfigurationException(
                $"{ProviderVariable} must be \"{Vultr}\" (the default) or \"{OpenRouter}\" (local tests only)."
            ),
        };
        var key =
            Value(keyVariable)
            ?? throw new InferenceConfigurationException(
                $"Set {keyVariable} in the environment to call {provider}. Keys are never read from files in the repository."
            );
        var effort = (Value(ReasoningVariable) ?? DefaultReasoningEffort).ToLowerInvariant();
        if (!Efforts.Contains(effort))
            throw new InferenceConfigurationException(
                $"{ReasoningVariable} must be one of {string.Join(", ", Efforts)}; the model cannot run without reasoning on Vultr."
            );
        return new(
            provider,
            Endpoint(baseUrl),
            Value(ModelVariable) ?? model,
            key,
            Number(Value(MaxOutputVariable), MaxOutputVariable, DefaultMaxOutputTokens, 1, 1_000_000),
            TimeSpan.FromSeconds(Number(Value(TimeoutVariable), TimeoutVariable, DefaultTimeoutSeconds, 5, 900)),
            effort
        );
    }

    // HTTPS only, except a loopback address so tests can stand in a local fake. No credentials in the URL.
    private static Uri Endpoint(string text)
    {
        if (
            !Uri.TryCreate(text.TrimEnd('/') + "/", UriKind.Absolute, out var url)
            || url.UserInfo.Length > 0
            || !(url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp && url.IsLoopback)
        )
            throw new InferenceConfigurationException(
                $"{VultrBaseUrlVariable} must be an https URL such as {VultrBaseUrl}."
            );
        return url;
    }

    private static int Number(string? text, string name, int fallback, int min, int max)
    {
        if (text is null)
            return fallback;
        if (int.TryParse(text, out var value) && value >= min && value <= max)
            return value;
        throw new InferenceConfigurationException($"{name} must be a whole number from {min} to {max}.");
    }

    // The key is deliberately left out so a logged settings record cannot leak it.
    public override string ToString() =>
        $"{Provider} {BaseUrl} model={Model} max_tokens={MaxOutputTokens} timeout={Timeout.TotalSeconds:0}s reasoning={ReasoningEffort}";
}

public sealed class InferenceConfigurationException(string message) : Exception(message);
