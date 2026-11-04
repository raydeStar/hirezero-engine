using System.Text.Json;
using HireZero.Agent;
using HireZero.Core;
using HireZero.Integrations.Inference;
using HireZero.Integrations.Sandbox;
using HireZero.Integrations.Web;

// One contained browser task, end to end, for the demo:
//
//   dotnet run --project tools/HireZero.Engine.Run -- --goal "What does this site sell?" --site https://example.com
//
// 1. The model (Vultr by default) drafts a read-only itinerary on the sites you name.
// 2. You approve that exact packet by typing the first 8 characters of its digest.
// 3. The qualified gVisor browser runs it once; every request it makes goes through the host broker and is logged.
// 4. The result is saved, the sandbox is torn down, and the model answers the goal from the captured text.
// 5. A receipt lists tokens, allowed and blocked requests, and whether teardown was confirmed.
//
// Needs, on the VM: HIREZERO_BROWSER_RECEIPT (the qualification receipt from deploy/vultr/qualify.sh), the Vultr
// inference key, and Docker at /usr/bin/docker. It refuses to run a browser that is not qualified.

string? Arg(string name) =>
    args.Select((a, i) => (a, i))
        .Where(p => p.a == name && p.i + 1 < args.Length)
        .Select(p => args[p.i + 1])
        .FirstOrDefault();

var goal = Arg("--goal");
var sites = args.Select((a, i) => (a, i))
    .Where(p => p.a == "--site" && p.i + 1 < args.Length)
    .Select(p => args[p.i + 1])
    .ToArray();
if (goal is null || sites.Length is < 1 or > 4)
{
    Console.Error.WriteLine("Usage: --goal \"<what to find out>\" --site https://<site> [--site ...] (1 to 4 sites)");
    return 2;
}

var runs = Path.GetFullPath(Arg("--runs") ?? "runs");
var receiptPath =
    Environment.GetEnvironmentVariable("HIREZERO_BROWSER_RECEIPT")
    ?? "/opt/hirezero-qualification/receipts/browser-qualification.json";
var docker = Environment.GetEnvironmentVariable("HIREZERO_BROWSER_DOCKER") ?? "/usr/bin/docker";
const string Endpoint = "unix:///var/run/docker.sock";

InferenceSettings settings;
InferenceBudget budget;
var usage = new InferenceUsageLog(Path.Combine(runs, "inference-usage.jsonl"));
try
{
    settings = InferenceSettings.FromEnvironment();
    budget = InferenceBudget.FromEnvironment(usage);
}
catch (InferenceConfigurationException error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}
if (settings.LocalTestOnly)
    Console.WriteLine($"Note: {settings.Provider} is the local-test provider; the competition run uses Vultr.");

var qualification = BrowserQualification.Read(receiptPath);
if (qualification is null)
{
    Console.Error.WriteLine($"No browser qualification receipt at {receiptPath}. Run deploy/vultr/qualify.sh first.");
    return 2;
}

var network = new BrowserNetworkLog(Path.Combine(runs, "network.jsonl"));
var commands = new DockerCommands(docker, Endpoint);
var browser = new QualifiedDockerBrowser(
    commands,
    qualification,
    () => BrowserQualification.Read(receiptPath),
    () => SandboxQualification.ExecutableHash(docker),
    enabled: true,
    (request, authorized) => new PublicBrowserNetwork(request, authorized, observe: network.Append)
);
await browser.RefreshAsync(CancellationToken.None);
if (!browser.Availability.Available)
{
    Console.Error.WriteLine($"The browser runtime is not ready ({browser.Availability.Status}); nothing was run.");
    return 2;
}

using var model = new InferenceClient(settings, budget);
var task = new BrowserTask(model, browser, runs);
Console.WriteLine($"Model: {settings.Provider} {settings.Model} at {settings.BaseUrl.Host}");
Console.WriteLine($"Goal: {goal}");

var began = DateTimeOffset.UtcNow;
var itinerary = await task.DraftAsync(goal, sites, CancellationToken.None);
Console.WriteLine();
Console.WriteLine($"Itinerary ({itinerary.Source}{(itinerary.Note is null ? "" : "; " + itinerary.Note)}):");
foreach (var (step, n) in itinerary.Request.Steps.Select((s, i) => (s, i + 1)))
    Console.WriteLine($"  {n}. {step.Action} {step.Value}");
Console.WriteLine($"  Sites: {string.Join(", ", itinerary.Request.Origins)}");
var l = itinerary.Request.Limits;
Console.WriteLine(
    $"  Limits: {l.Seconds} s, {l.Requests} requests, {l.Bytes / 1024 / 1024} MB; 512 MB memory, 1 CPU, no network"
);
Console.WriteLine($"  Packet digest: {itinerary.Digest}");

var approved = Arg("--approve");
if (approved is null)
{
    Console.Write("Approve this exact packet? Type the first 8 characters of its digest: ");
    var typed = Console.ReadLine()?.Trim() ?? "";
    approved =
        typed.Length == 8 && itinerary.Digest.StartsWith(typed, StringComparison.Ordinal) ? itinerary.Digest : "";
}
if (approved != itinerary.Digest)
{
    Console.WriteLine("Not approved; nothing was run.");
    return 1;
}

var outcome = await task.RunAsync(itinerary, approved, goal, CancellationToken.None);
var execution = outcome.Execution;
var events = network.Read().Where(e => e.JobId == itinerary.Request.JobId).ToArray();
var calls = usage.Read().Where(e => e.At >= began).ToArray();

Console.WriteLine();
Console.WriteLine(
    $"Browser: {execution.State}{(execution.Reason is null ? "" : " (" + execution.Reason + ")")}, {execution.WallMilliseconds ?? 0} ms"
);
foreach (var capture in execution.Result?.Captures ?? [])
    Console.WriteLine($"  read step {capture.Step}: {capture.Title} ({capture.Url}), {capture.Text.Length} characters");
Console.WriteLine(
    $"Network: {events.Count(e => e.Outcome == "allowed")} allowed, {events.Count(e => e.Outcome == "blocked")} blocked"
);
foreach (var blocked in events.Where(e => e.Outcome == "blocked"))
    Console.WriteLine($"  BLOCKED {blocked.Kind}: {blocked.Method} {blocked.Address} ({blocked.Reason})");
Console.WriteLine(
    $"Teardown: {(outcome.Release.Confirmed ? "confirmed" : "NOT confirmed")} ({outcome.Release.Status}, {outcome.Release.ResourceId})"
);
Console.WriteLine();
Console.WriteLine(outcome.Answer ?? outcome.AnswerNote);

var receipt = new
{
    itinerary.Request.JobId,
    goal,
    itinerary.Digest,
    itinerary.Source,
    provider = settings.Provider,
    model = settings.Model,
    browser = new
    {
        execution.State,
        execution.Reason,
        execution.WallMilliseconds,
        image = qualification.Image,
    },
    network = new
    {
        allowed = events.Count(e => e.Outcome == "allowed"),
        blocked = events
            .Where(e => e.Outcome == "blocked")
            .Select(e => new
            {
                e.Kind,
                e.Method,
                e.Address,
                e.Reason,
            }),
    },
    teardown = outcome.Release,
    modelCalls = calls.Select(c => new
    {
        c.Purpose,
        c.Outcome,
        c.InputTokens,
        c.OutputTokens,
        c.ReasoningTokens,
        c.ElapsedMs,
    }),
    tokensThisRun = budget.RunUsed,
};
File.WriteAllText(
    Path.Combine(outcome.RunFolder, "receipt.json"),
    JsonSerializer.Serialize(receipt, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true })
);
Console.WriteLine();
Console.WriteLine($"Receipt: {Path.Combine(outcome.RunFolder, "receipt.json")} ({budget.RunUsed:N0} tokens this run)");
return outcome.Release.Confirmed && execution.State == "completed" ? 0 : 1;
