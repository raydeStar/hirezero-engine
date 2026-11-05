namespace HireZero.Integrations.Inference;

// A call that would break a token limit is refused before anything is sent, so it has no usage.
public sealed class InferenceBudgetException(string message) : Exception(message);

// Two token limits on every model call: one for the current run (one task or one process) and one for the calendar
// month (UTC), counted from the usage log so it holds across runs. Each call reserves its worst case first (every
// input byte a token, plus framing, plus its full output limit). When the call ends, the reservation becomes what the
// provider reported, nothing for a refusal, or stays whole when the outcome is unknown, because an unconfirmed call
// may still have been billed.
public sealed class InferenceBudget
{
    public const string RunVariable = "HIREZERO_INFERENCE_RUN_TOKEN_LIMIT";
    public const string MonthlyVariable = "HIREZERO_INFERENCE_MONTHLY_TOKEN_LIMIT";

    // About a dozen full-size DeepSeek replies per run, and a month well inside the event's credits at Vultr's
    // published DeepSeek V4.1 Flash input price. Both are deliberately conservative; raise them in the environment.
    public const long DefaultRunTokens = 200_000;
    public const long DefaultMonthlyTokens = 20_000_000;

    // An upper bound on the tokens a chat API adds around the system and user text (role markers and separators).
    public const int FramingTokens = 256;

    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _now;
    private long _runCharged;
    private long _held;

    public InferenceBudget(long runLimit, long monthlyLimit, InferenceUsageLog log, Func<DateTimeOffset>? now = null)
    {
        if (runLimit < 1 || monthlyLimit < 1)
            throw new InferenceConfigurationException("Token limits must be positive.");
        RunLimit = runLimit;
        MonthlyLimit = monthlyLimit;
        Log = log;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public static InferenceBudget FromEnvironment(InferenceUsageLog log, Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        return new(
            Limit(read(RunVariable), RunVariable, DefaultRunTokens),
            Limit(read(MonthlyVariable), MonthlyVariable, DefaultMonthlyTokens),
            log
        );
    }

    public long RunLimit { get; }
    public long MonthlyLimit { get; }
    public InferenceUsageLog Log { get; }

    // Tokens this run has used, counting calls still in flight at their worst case.
    public long RunUsed
    {
        get
        {
            lock (_gate)
                return _runCharged + _held;
        }
    }

    // Tokens charged in the log this calendar month (UTC), plus calls still in flight.
    public long MonthUsed()
    {
        lock (_gate)
            return Charged() + _held;
    }

    public DateTimeOffset Now() => _now();

    public static long WorstCase(int inputBytes, int maxOutputTokens) =>
        (long)inputBytes + FramingTokens + maxOutputTokens;

    internal void Reserve(long worst)
    {
        lock (_gate)
        {
            if (_runCharged + _held + worst > RunLimit)
                throw new InferenceBudgetException(
                    $"This call could use up to {worst:N0} tokens, and this run has {Math.Max(0, RunLimit - _runCharged - _held):N0} of its {RunLimit:N0} left ({RunVariable}). Nothing was sent."
                );
            var month = Charged() + _held;
            if (month + worst > MonthlyLimit)
                throw new InferenceBudgetException(
                    $"This call could use up to {worst:N0} tokens, and this month has {Math.Max(0, MonthlyLimit - month):N0} of its {MonthlyLimit:N0} left ({MonthlyVariable}). Nothing was sent."
                );
            _held += worst;
        }
    }

    // Called once per reserved call, after its log entry (which carries the charge) has been written.
    internal void Settle(long worst, long charged)
    {
        lock (_gate)
        {
            _held -= worst;
            _runCharged += charged;
        }
    }

    private long Charged()
    {
        var now = _now().ToUniversalTime();
        return Log.Read()
            .Where(e => e.At.ToUniversalTime() is var at && at.Year == now.Year && at.Month == now.Month)
            .Sum(e => e.ChargedTokens);
    }

    private static long Limit(string? text, string name, long fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;
        if (long.TryParse(text.Trim(), out var value) && value is >= 1 and <= 10_000_000_000)
            return value;
        throw new InferenceConfigurationException($"{name} must be a whole number of tokens from 1 to 10,000,000,000.");
    }
}
