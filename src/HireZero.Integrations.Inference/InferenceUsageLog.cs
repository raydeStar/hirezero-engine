using System.Text.Json;

namespace HireZero.Integrations.Inference;

// One line per model call: who served it and how many tokens it used. No prompt, reply or key is ever written.
public sealed record InferenceUsageEntry(
    DateTimeOffset At,
    string Provider,
    string Host,
    string Model,
    string Purpose,
    string Outcome,
    long? InputTokens,
    long? OutputTokens,
    long? ReasoningTokens,
    long ElapsedMs,
    string? ResponseId,
    // What the call counts against the token limits: reported usage, nothing for a refusal, or the whole worst-case
    // reservation when the outcome is unknown.
    long ChargedTokens
);

// An append-only JSON Lines file. Appends are serialized so concurrent calls never interleave a line.
public sealed class InferenceUsageLog(string path)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public void Append(InferenceUsageEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, Options) + "\n";
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, line);
        }
    }

    public IReadOnlyList<InferenceUsageEntry> Read() =>
        File.Exists(Path)
            ? File.ReadAllLines(Path)
                .Where(line => line.Length > 0)
                .Select(line => JsonSerializer.Deserialize<InferenceUsageEntry>(line, Options)!)
                .ToArray()
            : [];
}
