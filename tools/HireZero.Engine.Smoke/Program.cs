using HireZero.Integrations.Inference;

// One live model call through the engine's inference client, to prove the configured provider answers. It spends a
// few thousand tokens at most, so it runs only with --live, and only against Vultr unless --local-test is given.
// Token use goes to receipts/inference-usage.jsonl (never committed) and to the console.
//
//   dotnet run --project tools/HireZero.Engine.Smoke -- --live
//   dotnet run --project tools/HireZero.Engine.Smoke -- --live --local-test   (OpenRouter stand-in, local only)

const int SmokeOutputTokens = 4000;

var live = args.Contains("--live");
var localTest = args.Contains("--local-test");

InferenceSettings settings;
try
{
    settings = InferenceSettings.FromEnvironment();
}
catch (InferenceConfigurationException error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

Console.WriteLine($"Provider: {settings}");
if (settings.LocalTestOnly && !localTest)
{
    Console.Error.WriteLine(
        $"{settings.Provider} is for local tests only. Unset {InferenceSettings.ProviderVariable} to use Vultr, or pass --local-test."
    );
    return 2;
}
if (!live)
{
    Console.WriteLine(
        $"Dry run: nothing sent. Pass --live to make one call (output capped at {SmokeOutputTokens} tokens)."
    );
    return 0;
}

var log = new InferenceUsageLog(Path.Combine("receipts", "inference-usage.jsonl"));
InferenceBudget budget;
try
{
    budget = InferenceBudget.FromEnvironment(log);
}
catch (InferenceConfigurationException error)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}
Console.WriteLine(
    $"Budget: run {budget.RunLimit:N0} tokens; month {budget.MonthUsed():N0} of {budget.MonthlyLimit:N0} used"
);
using var client = new InferenceClient(settings, budget);
try
{
    var reply = await client.CompleteAsync(
        "You are a connectivity check. Reply with the single word: pong",
        "ping",
        "smoke",
        SmokeOutputTokens
    );
    var u = reply.Usage;
    Console.WriteLine($"Reply: {reply.Text.Trim()}");
    Console.WriteLine(
        $"Served by {settings.BaseUrl.Host} ({reply.Model}), response {reply.ResponseId}, {reply.Elapsed.TotalSeconds:0.0} s"
    );
    Console.WriteLine(
        $"Tokens: {u.InputTokens} input, {u.OutputTokens} output ({u.ReasoningTokens?.ToString() ?? "unreported"} reasoning), {u.TotalTokens} total"
    );
    Console.WriteLine($"Logged to {log.Path}");
    return 0;
}
catch (InferenceBudgetException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (InferenceRefusedException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (InferenceUncertainException error)
{
    Console.Error.WriteLine(error.Message);
    if (error.Usage is { } u)
        Console.Error.WriteLine($"Tokens: {u.InputTokens} input, {u.OutputTokens} output");
    Console.Error.WriteLine($"Logged to {log.Path}. Not retried.");
    return 1;
}
