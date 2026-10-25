using CodexLiveUsage.Core;

var failures = new List<string>();

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures.Add($"{name}: {error.Message}");
        Console.WriteLine($"FAIL {name}: {error.Message}");
    }
}

const string FirstEvent =
    """{"timestamp":"2026-10-25T00:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"cached_input_tokens":50,"output_tokens":10,"total_tokens":110},"total_token_usage":{"input_tokens":100,"cached_input_tokens":50,"output_tokens":10,"total_tokens":110},"model_context_window":1000}}}""";

const string SecondEvent =
    """{"timestamp":"2026-10-25T00:00:02Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":300,"cached_input_tokens":200,"output_tokens":30,"total_tokens":330},"total_token_usage":{"input_tokens":400,"cached_input_tokens":250,"output_tokens":40,"total_tokens":440},"model_context_window":1000}}}""";

Run("parses the latest token event and cumulative speed", () =>
{
    var snapshot = RolloutMetricsParser.Parse([FirstEvent, SecondEvent]);

    TestAssert.Equal(330d, snapshot?.ContextUsed);
    TestAssert.Equal(1000d, snapshot?.ContextLimit);
    TestAssert.Equal(33d, snapshot?.ContextPercent);
    TestAssert.Equal(66.7d, snapshot?.CacheHitPercent);
    TestAssert.Equal(300d, snapshot?.LastInput);
    TestAssert.Equal(30d, snapshot?.LastOutput);
    TestAssert.Equal(15d, snapshot?.OutputTokensPerSecond);
    TestAssert.Equal(440d, snapshot?.SessionTotal);
});

Run("ignores malformed and unrelated lines", () =>
{
    TestAssert.Null(RolloutMetricsParser.Parse(["{not-json", """{"type":"message"}"""]));
});

Run("returns an empty parse as null", () =>
{
    TestAssert.Null(RolloutMetricsParser.Parse(Array.Empty<string>()));
});

Run("does not invent a context percentage without a limit", () =>
{
    const string noLimit =
        """{"timestamp":"2026-10-25T00:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":10,"cached_input_tokens":5,"output_tokens":2,"total_tokens":12},"total_token_usage":{"input_tokens":10,"cached_input_tokens":5,"output_tokens":2,"total_tokens":12}}}}""";

    var snapshot = RolloutMetricsParser.Parse([noLimit]);
    TestAssert.Equal(12d, snapshot?.ContextUsed);
    TestAssert.Null(snapshot?.ContextLimit);
    TestAssert.Null(snapshot?.ContextPercent);
});

Run("clamps cache hit percentage to 100", () =>
{
    const string oversizedCache =
        """{"timestamp":"2026-10-25T00:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":10,"cached_input_tokens":20,"output_tokens":2,"total_tokens":12},"total_token_usage":{"input_tokens":10,"cached_input_tokens":20,"output_tokens":2,"total_tokens":12},"model_context_window":100}}}""";

    TestAssert.Equal(100d, RolloutMetricsParser.Parse([oversizedCache])?.CacheHitPercent);
});

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("All tests passed.");
return 0;

static class TestAssert
{
    public static void Equal(double expected, double? actual)
    {
        if (actual is null || Math.Abs(expected - actual.Value) > 0.0001)
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual?.ToString() ?? "null"}.");
        }
    }

    public static void Null(double? actual)
    {
        if (actual is not null)
        {
            throw new InvalidOperationException($"Expected null, got {actual}.");
        }
    }

    public static void Null(object? actual)
    {
        if (actual is not null)
        {
            throw new InvalidOperationException($"Expected null, got {actual}.");
        }
    }
}
