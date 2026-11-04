using System.Text;
using System.Text.Json;
using HireZero.Agent;
using HireZero.Core;
using HireZero.Integrations.Inference;
using HireZero.Integrations.Web;

namespace HireZero.Engine.Web;

internal sealed record Review(
    string Goal,
    string Site,
    string Session,
    string Runtime,
    bool Simulated,
    long DraftTokens,
    DraftedItinerary Packet
);

internal sealed record FinalEvidence(
    string State,
    string? Answer,
    string? AnswerNote,
    long Tokens,
    long? WallMilliseconds,
    SandboxRelease Teardown
);

internal sealed record CaptureView(int Step, string Title, string Url, string Text, string? ScreenshotUrl);

internal sealed record Claim(string Operation, string? JobId);

internal sealed class WebWorker : IDisposable
{
    private readonly WebOptions options;
    private readonly string root;
    private readonly FileStream instance;
    private readonly SemaphoreSlim gate = new(1);
    private readonly InferenceUsageLog usage;

    private sealed record Pending(string Id, string Session, string ReviewDigest, WebRuntime Runtime);

    private Pending? current;
    private string? running;
    private string ClaimPath => Path.Combine(root, "active-operation.json");

    private string Job(string id) => Path.Combine(root, "jobs", id);

    private string Run(string id) => Path.Combine(root, "runs", id);

    public WebWorker(WebOptions options)
    {
        this.options = options;
        root = options.Data!;
        Directory.CreateDirectory(root);
        if (!Files.Plain(root))
            throw new ArgumentException("Private data must be an ordinary directory.");
        instance = new FileStream(
            Path.Combine(root, "worker.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        try
        {
            var mode = Path.Combine(root, "mode.txt");
            if (File.Exists(mode) && File.ReadAllText(mode) != options.Mode)
                throw new ArgumentException("Use distinct data roots for rehearsal and the qualified worker.");
            File.WriteAllText(mode, options.Mode);
            Directory.CreateDirectory(Path.Combine(root, "jobs"));
        }
        catch
        {
            instance.Dispose();
            throw;
        }
        usage = new(Path.Combine(root, "inference-usage.jsonl"));
    }

    private void Enter()
    {
        if (!gate.Wait(0))
            throw new WorkspaceException("Another owner operation is in progress.", 409);
    }

    private void CheckClear()
    {
        if (File.Exists(ClaimPath))
            throw new WorkspaceException("An earlier operation needs operator review before another paid action.", 409);
    }

    public async Task<object> Draft(string? goal, string? site, string session)
    {
        goal = ContentGuard.Bounded(goal, 1000);
        var uri = BrowserRequest.Address(site ?? "");
        if (uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new WorkspaceException("Enter one public HTTPS origin without a path, query or fragment.", 422);
        site = BrowserRequest.Origin(uri);
        Enter();
        WebRuntime? runtime = null;
        try
        {
            CheckClear();
            runtime = await WebRuntime.Create(options, usage);
            Files.NewJson(ClaimPath, new Claim("draft", null));
            var packet = await runtime.Task.DraftAsync(goal, [site], CancellationToken.None);
            var id = packet.Request.JobId;
            var review = new Review(
                goal,
                site,
                session,
                runtime.Identity,
                options.Mode == "rehearsal",
                runtime.Model.Budget.RunUsed,
                packet
            );
            Directory.CreateDirectory(Job(id));
            Files.NewJson(Path.Combine(Job(id), "draft.json"), review);
            File.Delete(ClaimPath);
            Interlocked.Exchange(ref current, new(id, session, CompanyJson.Digest(review), runtime))?.Runtime.Dispose();
            runtime = null;
            return new
            {
                jobId = id,
                packet.Digest,
                goal,
                site,
                packet.Source,
                packet.Note,
                packet.Request,
                review.Simulated,
            };
        }
        finally
        {
            runtime?.Dispose();
            gate.Release();
        }
    }

    private Review ReadReview(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _) || id != id.ToLowerInvariant())
            throw new WorkspaceException("Unknown job.", 404);
        var review = Files.ReadJson<Review>(Path.Combine(Job(id), "draft.json"), 64 * 1024);
        if (review is null || review.Packet.Request.JobId != id)
            throw new WorkspaceException("Unknown job.", 404);
        return review;
    }

    public async Task<object> Approve(string id, string digest, string session)
    {
        Enter();
        try
        {
            CheckClear();
            var pending = Volatile.Read(ref current);
            if (pending is null || pending.Id != id || pending.Session != session)
                throw new WorkspaceException("This packet is no longer pending in this session.", 409);
            var review = ReadReview(id);
            var packet = review.Packet;
            if (
                review.Session != session
                || CompanyJson.Digest(review) != pending.ReviewDigest
                || digest != packet.Digest
            )
                throw new WorkspaceException("The approval does not match the saved packet.", 403);
            packet.Request.Validate();
            Files.NewJson(ClaimPath, new Claim("approve", id));
            Files.NewJson(Path.Combine(Job(id), "consumed.json"), new { digest, at = DateTimeOffset.UtcNow });
            Volatile.Write(ref running, id);
            Interlocked.Exchange(ref current, null);
            using var runtime = pending.Runtime;
            var outcome = await runtime.Task.RunAsync(packet, digest, review.Goal, CancellationToken.None);
            var final = new FinalEvidence(
                outcome.Execution.State,
                outcome.Answer,
                outcome.AnswerNote,
                runtime.Model.Budget.RunUsed,
                outcome.Execution.WallMilliseconds,
                outcome.Release
            );
            Files.NewJson(Path.Combine(Job(id), "result.json"), final);
            if (outcome.Release.Confirmed)
                File.Delete(ClaimPath);
            return Result(id, session);
        }
        finally
        {
            Volatile.Write(ref running, null);
            gate.Release();
        }
    }

    private static T? Saved<T>(string path, int limit)
        where T : class
    {
        // A live or interrupted engine write may not yet contain a complete record.
        try
        {
            return Files.ReadJson<T>(path, limit);
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return null;
        }
    }

    private bool CanApprove(string id, string session) =>
        Volatile.Read(ref current) is { } c && c.Id == id && c.Session == session && !File.Exists(ClaimPath);

    private string State(string id, string session, FinalEvidence? final) =>
        final is not null
            ? final.Teardown.Confirmed
                ? final.State
                : "teardown-unconfirmed"
            : Volatile.Read(ref running) == id
                ? "running"
                : File.Exists(Path.Combine(Job(id), "consumed.json"))
                    ? "interrupted"
                    : CanApprove(id, session)
                        ? "draft"
                        : "expired";

    public object[] List(string session) =>
        Directory
            .EnumerateDirectories(Path.Combine(root, "jobs"))
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(id => Guid.TryParseExact(id, "N", out _))
            .Take(50)
            .Select(id =>
            {
                var d = ReadReview(id);
                return new
                {
                    jobId = id,
                    d.Goal,
                    state = State(id, session, Saved<FinalEvidence>(Path.Combine(Job(id), "result.json"), 128 * 1024)),
                    d.Simulated,
                };
            })
            .ToArray();

    private IEnumerable<(BrowserCapture Capture, string? Path)> Captures(string id, BrowserExecution? execution)
    {
        var shot = 0;
        foreach (var c in execution?.Result?.Captures ?? [])
        {
            var path = Path.Combine(Run(id), $"capture-{c.Step}-{shot + 1}.jpg");
            if (c.Step is >= 1 and <= 16 && File.Exists(path) && Files.Plain(path))
            {
                shot++;
                yield return (c, path);
            }
            else
                yield return (c, null);
        }
    }

    public byte[]? Screenshot(string id, int step)
    {
        ReadReview(id);
        var e = Saved<BrowserExecution>(Path.Combine(Run(id), "execution.json"), 2 * 1024 * 1024);
        var path = Captures(id, e).FirstOrDefault(c => c.Capture.Step == step).Path;
        return path is null ? null : Files.Jpeg(path);
    }

    public object Result(string id, string session)
    {
        var d = ReadReview(id);
        var e = Saved<BrowserExecution>(Path.Combine(Run(id), "execution.json"), 2 * 1024 * 1024);
        var final = Saved<FinalEvidence>(Path.Combine(Job(id), "result.json"), 128 * 1024);
        var release = final?.Teardown ?? Saved<SandboxRelease>(Path.Combine(Run(id), "release.json"), 16 * 1024);
        var captures = Captures(id, e)
            .Select(x => new CaptureView(
                x.Capture.Step,
                x.Capture.Title,
                x.Capture.Url,
                x.Capture.Text,
                x.Path is null ? null : $"/api/jobs/{id}/captures/{x.Capture.Step}"
            ))
            .ToArray();
        var events = new BrowserNetworkLog(Path.Combine(root, "network", id + ".jsonl"))
            .Read()
            .Where(n => n.JobId == id)
            .Take(110)
            .ToArray();
        var origins = (e?.Result?.Blocked ?? [])
            .Where(b => b.Reason == "This request leaves the approved browser websites.")
            .Select(b => b.Url)
            .Concat(events.Where(b => b.Kind == "off-site" && b.Outcome == "blocked").Select(b => b.Address))
            .Select(url =>
                Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Scheme == "https"
                && uri.IsDefaultPort
                && uri.HostNameType == UriHostNameType.Dns
                    ? BrowserRequest.Origin(uri)
                    : null
            )
            .OfType<string>()
            .Except(d.Packet.Request.Origins, StringComparer.Ordinal)
            .ToArray();
        var savedAnswer = final is null ? Files.Read(Path.Combine(Run(id), "answer.md"), 96 * 1024) : null;
        return new
        {
            jobId = id,
            d.Goal,
            d.Site,
            d.Packet.Digest,
            d.Packet.Request,
            d.Packet.Source,
            d.Packet.Note,
            canApprove = CanApprove(id, session),
            state = State(id, session, final),
            answer = final?.Answer ?? (savedAnswer is null ? null : Encoding.UTF8.GetString(savedAnswer)),
            answerNote = final?.AnswerNote,
            teardown = new { confirmed = release?.Confirmed ?? false, status = release?.Status ?? "unconfirmed" },
            tokens = final?.Tokens
                ?? (File.Exists(Path.Combine(Job(id), "consumed.json")) ? (long?)null : d.DraftTokens),
            wallMilliseconds = final?.WallMilliseconds ?? e?.WallMilliseconds,
            blockedOrigins = origins,
            events = events.Select(n => new
            {
                n.Outcome,
                n.Kind,
                n.Address,
                n.Reason,
            }),
            captures,
            d.Simulated,
        };
    }

    public void Dispose()
    {
        current?.Runtime.Dispose();
        instance.Dispose();
        gate.Dispose();
    }
}
