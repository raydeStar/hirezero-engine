using System.Text.Json;

namespace HireZero.Integrations.Web;

// One request the browser asked the broker to make, and what the broker did with it. Kind says why a request was
// blocked: "off-site" (outside the approved websites), "address" (not a public https address), "method" (not a GET),
// "revoked", "deadline", "unreadable", or "refused" for the rest (limits, crawl policy, content type); the reason
// says which. Allowed requests are "page" or "redirect". The address
// keeps only scheme, host, port and path: queries and fragments can carry data, so they are never written.
public sealed record BrowserNetworkEvent(
    DateTimeOffset At,
    string JobId,
    string Outcome,
    string Kind,
    string Method,
    string Address,
    int? Status,
    string? Reason
)
{
    public const string Allowed = "allowed";
    public const string Blocked = "blocked";

    public static string Redact(string? address)
    {
        if (string.IsNullOrEmpty(address))
            return "";
        if (Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) && uri.Host.Length > 0)
        {
            var text = uri.GetLeftPart(UriPartial.Path);
            return text.Length <= 300 ? text : text[..300];
        }
        var cut = address.Split('?', '#')[0];
        return "(not an address) " + (cut.Length <= 80 ? cut : cut[..80]);
    }
}

// An append-only JSON Lines file of broker events, one per line, safe for concurrent appends.
public sealed class BrowserNetworkLog(string path)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public void Append(BrowserNetworkEvent entry)
    {
        var line = JsonSerializer.Serialize(entry, Options) + "\n";
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, line);
        }
    }

    public IReadOnlyList<BrowserNetworkEvent> Read() =>
        File.Exists(Path)
            ? File.ReadAllLines(Path)
                .Where(line => line.Length > 0)
                .Select(line => JsonSerializer.Deserialize<BrowserNetworkEvent>(line, Options)!)
                .ToArray()
            : [];
}
