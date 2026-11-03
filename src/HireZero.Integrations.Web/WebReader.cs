using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace HireZero.Integrations.Web;

// The parts of HireZero's web.read tool that the browser broker uses, copied unchanged except that refusals are a
// plain WorkspaceException (HireZero's ToolFailureException derives from it with the same message and status):
// - only https URLs with no credentials, at most three redirects, each checked again;
// - every address is checked when the socket connects, so a name that resolves to loopback, a private network,
//   link-local, multicast or another non-public range is refused even if it changes between lookups;
// - one request at a time with a courtesy delay, and robots.txt rules for the reader's agent.
public sealed partial class WebReader
{
    public const int MaxRedirects = 3;

    // Serialize research across reader instances. A courteous butler knocks once, then waits.
    private static readonly SemaphoreSlim ResearchGate = new(1, 1);
    private static DateTimeOffset NextResearchRequest;
    private static DateTimeOffset LastResearchRequest;
    private const string Agent = "HireZero-source-reader";

    private static WorkspaceException Refused(string message) => new(message, 422);

    // An absolute https URL with a host name or address, no credentials in it.
    public static Uri Checked(string text)
    {
        if (
            !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps
            || url.UserInfo.Length > 0
            || url.Host.Length == 0
        )
            throw Refused("Use a public https address, such as https://example.com/.");
        if (IPAddress.TryParse(url.Host.Trim('[', ']'), out var literal) && !IsPublic(literal))
            throw Refused("That address is not on the public internet.");
        return url;
    }

    // Connects only to public addresses: the check happens on the address actually used for the socket.
    private static async ValueTask<Stream> ConnectPublicAsync(
        SocketsHttpConnectionContext context,
        CancellationToken token
    )
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
        var allowed = addresses.Where(IsPublic).ToArray();
        if (allowed.Length == 0)
            throw Refused("That address is not on the public internet.");
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Public unicast only: no loopback, private, shared, link-local, multicast, documentation or reserved ranges.
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(
                b[0] is 0 or 10 or 127 or >= 224
                || b[0] == 100 && b[1] is >= 64 and <= 127
                || b[0] == 169 && b[1] == 254
                || b[0] == 172 && b[1] is >= 16 and <= 31
                || b[0] == 192 && b[1] == 168
                || b[0] == 192 && b[1] == 0 && b[2] is 0 or 2
                || b[0] == 198 && b[1] is 18 or 19
                || b[0] == 198 && b[1] == 51 && b[2] == 100
                || b[0] == 203 && b[1] == 0 && b[2] == 113
            );
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(
                IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.IPv6None)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC
                || b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8
            );
        }
        return false;
    }

    private static async Task PaceResearchAsync(CancellationToken token, double seconds = 1)
    {
        var earliest = LastResearchRequest.AddSeconds(Math.Max(1, seconds));
        if (NextResearchRequest > earliest)
            earliest = NextResearchRequest;
        var wait = earliest - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, token);
        LastResearchRequest = DateTimeOffset.UtcNow;
        NextResearchRequest = LastResearchRequest.AddSeconds(Math.Max(1, seconds));
    }

    // Longest matching agent group, then longest matching path; Allow wins ties. Unknown directives confer no access.
    public static (bool Allowed, double Delay) ResearchRules(string text, Uri url)
    {
        if (text.Length > 128 * 1024 || text.Count(c => c == '\n') > 2000)
            return (false, 1);
        var groups = new List<(List<string> Agents, List<(string Name, string Value)> Rules)>();
        List<string> agents = [];
        List<(string Name, string Value)> rules = [];
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#', 2)[0].Trim().TrimStart('\uFEFF');
            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;
            var name = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            if (name == "user-agent")
            {
                if (rules.Count > 0)
                {
                    groups.Add((agents, rules));
                    agents = [];
                    rules = [];
                }
                agents.Add(value);
            }
            else if (agents.Count > 0)
                rules.Add((name, value));
        }
        groups.Add((agents, rules));
        int Score(string candidate) =>
            candidate == "*" ? 0
            : Agent.Contains(candidate, StringComparison.OrdinalIgnoreCase) && candidate.Length > 0 ? candidate.Length
            : -1;
        var best = groups.SelectMany(g => g.Agents).Select(Score).DefaultIfEmpty(-1).Max();
        if (best < 0)
            return (true, 1);
        var selected = groups.Where(g => g.Agents.Any(a => Score(a) == best)).SelectMany(g => g.Rules).ToArray();
        var path = Uri.UnescapeDataString(url.PathAndQuery);
        var matches = new List<(int Length, bool Allow)>();
        var delay = 1.0;
        foreach (var rule in selected)
        {
            if (
                rule.Name == "crawl-delay"
                && double.TryParse(
                    rule.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var seconds
                )
            )
                delay = Math.Max(delay, double.IsFinite(seconds) ? seconds : 60);
            if (rule.Name is not ("allow" or "disallow") || rule.Value.Length == 0)
                continue;
            var pattern = Uri.UnescapeDataString(rule.Value);
            var end = pattern.EndsWith('$');
            if (end)
                pattern = pattern[..^1];
            var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + (end ? "$" : "");
            try
            {
                if (Regex.IsMatch(path, expression, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    matches.Add((pattern.Count(c => c != '*'), rule.Name == "allow"));
            }
            catch (RegexMatchTimeoutException)
            {
                return (false, delay);
            }
        }
        var longest = matches.Select(m => m.Length).DefaultIfEmpty(-1).Max();
        return (longest < 0 || matches.Any(m => m.Length == longest && m.Allow), delay);
    }
}
