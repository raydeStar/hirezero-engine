using System.Net;
using System.Text;
using HireZero.Core;
using HireZero.Integrations.Web;

public sealed class BrowserNetworkLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hz-network-" + Guid.NewGuid().ToString("N"));

    private sealed class Pages : HttpMessageHandler
    {
        public List<string> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Seen.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(
                request.RequestUri.AbsolutePath == "/robots.txt"
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<p>Fictional page</p>", Encoding.UTF8, "text/html"),
                    }
            );
        }
    }

    private const string JobId = "0123456789abcdef0123456789abcdef";

    private static BrowserRequest Scope() =>
        new(
            JobId,
            "co-fictional",
            ["https://paper.example.invalid"],
            [new("navigate", "https://paper.example.invalid/"), new("read", "body")],
            new(120)
        );

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnOffSiteRequestIsBlockedNeverSentAndLoggedWithoutItsQuery()
    {
        var log = new BrowserNetworkLog(Path.Combine(_folder, "network.jsonl"));
        var pages = new Pages();
        using var network = new PublicBrowserNetwork(Scope(), () => true, pages, log.Append);

        var reply = await network.FetchAsync(
            new("https://collector.example.invalid/beacon?session=fictional-private-value", "GET"),
            Token
        );

        Assert.NotNull(reply.Refusal);
        Assert.Empty(pages.Seen);
        var entry = Assert.Single(log.Read());
        Assert.Equal(
            (JobId, "blocked", "off-site", "GET", "https://collector.example.invalid/beacon"),
            (entry.JobId, entry.Outcome, entry.Kind, entry.Method, entry.Address)
        );
        Assert.Contains("approved browser websites", entry.Reason);
        Assert.DoesNotContain("fictional-private-value", File.ReadAllText(log.Path));
    }

    [Fact]
    public async Task PrivateAddressesAndWritesAreBlockedWithTheirOwnKinds()
    {
        var events = new List<BrowserNetworkEvent>();
        var pages = new Pages();
        using var network = new PublicBrowserNetwork(Scope(), () => true, pages, events.Add);

        await network.FetchAsync(new("https://169.254.169.254/latest/meta-data/", "GET"), Token);
        await network.FetchAsync(new("https://paper.example.invalid/submit", "POST"), Token);

        Assert.Equal(["address", "method"], events.Select(e => e.Kind));
        Assert.All(events, e => Assert.Equal("blocked", e.Outcome));
        Assert.Empty(pages.Seen);
        Assert.Equal(2, network.Usage.Refused);
    }

    [Fact]
    public async Task AnApprovedPageIsLoggedAsAllowed()
    {
        var events = new List<BrowserNetworkEvent>();
        using var network = new PublicBrowserNetwork(Scope(), () => true, new Pages(), events.Add);

        var reply = await network.FetchAsync(new("https://paper.example.invalid/", "GET"), Token);

        Assert.Null(reply.Refusal);
        var entry = Assert.Single(events);
        Assert.Equal(("allowed", "page", 200), (entry.Outcome, entry.Kind, entry.Status));
    }

    [Fact]
    public async Task AFailingLogNeverTurnsABlockIntoAnAllowedRequest()
    {
        var pages = new Pages();
        using var network = new PublicBrowserNetwork(
            Scope(),
            () => true,
            pages,
            _ => throw new IOException("Fictional full disk.")
        );

        var reply = await network.FetchAsync(new("https://collector.example.invalid/", "GET"), Token);

        Assert.NotNull(reply.Refusal);
        Assert.Empty(pages.Seen);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
