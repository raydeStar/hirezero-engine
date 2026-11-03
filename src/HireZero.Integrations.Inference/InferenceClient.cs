using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HireZero.Integrations.Inference;

public sealed record InferenceUsage(long InputTokens, long OutputTokens, long? ReasoningTokens)
{
    public long TotalTokens => InputTokens + OutputTokens;
}

public sealed record InferenceReply(
    string Text,
    InferenceUsage Usage,
    string Provider,
    string Model,
    string ResponseId,
    TimeSpan Elapsed
);

// The provider received the request and said no (4xx): nothing was processed, so no usage.
public sealed class InferenceRefusedException(string message) : Exception(message);

// The request may have been processed, but the reply is unusable (truncated, filtered, malformed) or the outcome is
// unknown (5xx, timeout, dropped connection). Never retried automatically; usage is attached when the provider sent it.
public sealed class InferenceUncertainException(string message, InferenceUsage? usage = null, Exception? inner = null)
    : Exception(message, inner)
{
    public InferenceUsage? Usage { get; } = usage;
}

// Every model call the engine makes goes through this one OpenAI-compatible client, pointed at the provider in
// InferenceSettings (Vultr by default). One call is one request: no redirects, no retries, max_tokens always set,
// and each call is appended to the usage log with its token counts but never its prompt, reply or key.
public sealed class InferenceClient : IDisposable
{
    private readonly InferenceSettings _settings;
    private readonly HttpClient _http;
    private readonly InferenceUsageLog? _log;

    public InferenceClient(
        InferenceSettings settings,
        InferenceUsageLog? log = null,
        HttpMessageHandler? handler = null
    )
    {
        _settings = settings;
        _log = log;
        _http = new HttpClient(
            handler
                ?? new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                },
            disposeHandler: handler is null
        )
        {
            BaseAddress = settings.BaseUrl,
            Timeout = settings.Timeout,
        };
    }

    public InferenceSettings Settings => _settings;

    public async Task<InferenceReply> CompleteAsync(
        string system,
        string user,
        string purpose,
        int? maxOutputTokens = null,
        CancellationToken token = default
    )
    {
        var limit = Math.Min(maxOutputTokens ?? _settings.MaxOutputTokens, _settings.MaxOutputTokens);
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(maxOutputTokens), "Every call needs a positive output limit.");
        var body = new JsonObject
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = limit,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = user }
            ),
        };
        // Vultr takes the OpenAI-style effort field; OpenRouter takes its own reasoning object and reports usage only
        // when asked.
        if (_settings.Provider == InferenceSettings.OpenRouter)
        {
            body["reasoning"] = new JsonObject { ["effort"] = _settings.ReasoningEffort };
            body["usage"] = new JsonObject { ["include"] = true };
        }
        else
            body["reasoning_effort"] = _settings.ReasoningEffort;

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new("Bearer", _settings.ApiKey);

        var clock = Stopwatch.StartNew();
        string text;
        int status;
        try
        {
            using var response = await _http.SendAsync(request, token);
            status = (int)response.StatusCode;
            text = await response.Content.ReadAsStringAsync(token);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException)
        {
            var reason = error is TaskCanceledException && !token.IsCancellationRequested ? "timed out" : "failed";
            Record(purpose, clock.Elapsed, "unknown", null, null);
            throw new InferenceUncertainException(
                $"The {_settings.Provider} request {reason}; the outcome is unknown and it was not retried.",
                inner: error
            );
        }

        if (status is >= 400 and < 500)
        {
            Record(purpose, clock.Elapsed, "refused-" + status, null, null);
            throw new InferenceRefusedException(
                $"{_settings.Provider} refused the request (HTTP {status}{ErrorType(text)}). Nothing was processed."
            );
        }
        if (status is < 200 or >= 300)
        {
            Record(purpose, clock.Elapsed, "unknown-" + status, null, null);
            throw new InferenceUncertainException(
                $"{_settings.Provider} failed (HTTP {status}); the outcome is unknown and it was not retried."
            );
        }

        string reply,
            stop,
            id;
        InferenceUsage usage;
        try
        {
            (reply, stop, usage, id) = Parse(text);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Record(purpose, clock.Elapsed, "malformed", null, null);
            throw new InferenceUncertainException(
                $"{_settings.Provider} returned a reply this client cannot read; it was not retried.",
                inner: error
            );
        }
        Record(purpose, clock.Elapsed, stop == "complete" ? "complete" : "withheld", usage, id);
        if (stop != "complete")
            throw new InferenceUncertainException(stop, usage);
        return new(reply, usage, _settings.Provider, _settings.Model, id, clock.Elapsed);
    }

    private void Record(string purpose, TimeSpan elapsed, string outcome, InferenceUsage? usage, string? id) =>
        _log?.Append(
            new InferenceUsageEntry(
                DateTimeOffset.UtcNow,
                _settings.Provider,
                _settings.BaseUrl.Host,
                _settings.Model,
                purpose,
                outcome,
                usage?.InputTokens,
                usage?.OutputTokens,
                usage?.ReasoningTokens,
                (long)elapsed.TotalMilliseconds,
                id
            )
        );

    private static (string Text, string Stop, InferenceUsage Usage, string Id) Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var choice = root.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var content =
            message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : "";
        var finish = choice.TryGetProperty("finish_reason", out var f) ? f.GetString() : null;
        var stop =
            message.TryGetProperty("tool_calls", out var tools)
            && tools.ValueKind == JsonValueKind.Array
            && tools.GetArrayLength() > 0
                ? "The model asked for a tool this call did not offer, so the reply was withheld."
                : finish switch
                {
                    "stop" => "complete",
                    "length" => "The reply reached its output limit and was withheld as truncated.",
                    "content_filter" => "The provider filtered the reply; it was withheld.",
                    _ => "The reply ended unexpectedly (" + finish + ") and was withheld.",
                };
        var u = root.GetProperty("usage");
        long? reasoning =
            u.TryGetProperty("completion_tokens_details", out var d)
            && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("reasoning_tokens", out var r)
            && r.ValueKind == JsonValueKind.Number
                ? r.GetInt64()
                : null;
        return (
            content,
            stop,
            new(u.GetProperty("prompt_tokens").GetInt64(), u.GetProperty("completion_tokens").GetInt64(), reasoning),
            root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : ""
        );
    }

    private static string ErrorType(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var error = document.RootElement.TryGetProperty("error", out var e) ? e : default;
            var type =
                error.ValueKind == JsonValueKind.Object && error.TryGetProperty("type", out var t) ? t.GetString()
                : error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var c) ? c.ToString()
                : null;
            return type is { Length: > 0 and <= 60 } ? ", " + type : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    public void Dispose() => _http.Dispose();
}
