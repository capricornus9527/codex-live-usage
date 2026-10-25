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

Run("reads only complete appended rollout lines", () =>
{
    using var directory = TestDirectory.Create();
    var sessions = Path.Combine(directory.Path, "sessions", "2026", "10", "25");
    Directory.CreateDirectory(sessions);
    var rollout = Path.Combine(sessions, "rollout-incremental.jsonl");
    File.WriteAllText(rollout, FirstEvent + Environment.NewLine);

    var monitor = new RolloutMetricsMonitor(directory.Path);
    TestAssert.Equal(110d, monitor.Poll()?.ContextUsed);

    var splitAt = Math.Min(48, SecondEvent.Length);
    File.AppendAllText(rollout, SecondEvent[..splitAt]);
    TestAssert.Equal(110d, monitor.Poll()?.ContextUsed);

    File.AppendAllText(rollout, SecondEvent[splitAt..] + Environment.NewLine);
    var latest = monitor.Poll();
    TestAssert.Equal(330d, latest?.ContextUsed);
    TestAssert.Equal(15d, latest?.OutputTokensPerSecond);
});

Run("switches rollout files and resets after truncation", () =>
{
    using var directory = TestDirectory.Create();
    var sessions = Path.Combine(directory.Path, "sessions", "2026", "10", "25");
    Directory.CreateDirectory(sessions);
    var firstFile = Path.Combine(sessions, "rollout-first.jsonl");
    var secondFile = Path.Combine(sessions, "rollout-second.jsonl");
    File.WriteAllText(firstFile, FirstEvent + Environment.NewLine);
    File.SetLastWriteTimeUtc(firstFile, new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc));

    var monitor = new RolloutMetricsMonitor(directory.Path);
    TestAssert.Equal(110d, monitor.Poll()?.ContextUsed);

    File.WriteAllText(secondFile, SecondEvent + Environment.NewLine);
    File.SetLastWriteTimeUtc(secondFile, new DateTime(2026, 10, 25, 0, 1, 0, DateTimeKind.Utc));
    TestAssert.Equal(330d, monitor.Poll()?.ContextUsed);

    const string shorterEvent =
        """{"timestamp":"2026-10-25T00:02:00Z","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"total_tokens":1500},"total_token_usage":{"total_tokens":1500},"model_context_window":2000}}}""";
    File.WriteAllText(secondFile, shorterEvent + Environment.NewLine);
    File.SetLastWriteTimeUtc(secondFile, new DateTime(2026, 10, 25, 0, 2, 0, DateTimeKind.Utc));
    TestAssert.Equal(1500d, monitor.Poll()?.ContextUsed);
    TestAssert.Equal(75d, monitor.Poll()?.ContextPercent);
});

Run("clamps an oversized line below the composer", () =>
{
    var placed = OverlayPlacement.BelowComposer(
        new PixelRect(100, 100, 400, 300),
        new PixelRect(100, 150, 400, 100),
        new PixelSize(800, 20),
        6);

    TestAssert.NotNull(placed);
    TestAssert.Equal(100, placed!.Value.Left);
    TestAssert.Equal(400, placed.Value.Width);
    TestAssert.Equal(256, placed.Value.Top);
    TestAssert.Equal(500, placed.Value.Right);
});

Run("fixes the transparent usage line directly below the composer", () =>
{
    var placed = OverlayPlacement.BelowComposer(
        new PixelRect(100, 100, 900, 700),
        new PixelRect(220, 650, 650, 100),
        new PixelSize(500, 18),
        6);

    TestAssert.NotNull(placed);
    TestAssert.Equal(295, placed!.Value.Left);
    TestAssert.Equal(756, placed.Value.Top);
    TestAssert.Equal(795, placed.Value.Right);
    TestAssert.True(placed.Value.Top > 750);
});

Run("hides the usage line when there is no room below the composer", () =>
{
    var placed = OverlayPlacement.BelowComposer(
        new PixelRect(100, 100, 900, 700),
        new PixelRect(220, 650, 650, 140),
        new PixelSize(500, 18),
        6);

    TestAssert.Null(placed);
});

Run("formats the usage line with Codex++ labels and compact tokens", () =>
{
    var text = MetricsFormatter.Format(new MetricsSnapshot(
        ContextUsed: 110_900,
        ContextLimit: 1_048_576,
        ContextPercent: 10.6,
        CacheHitPercent: 92,
        LastInput: 108_000,
        LastOutput: 146,
        LastTotal: 110_900,
        OutputTokensPerSecond: 33.5,
        SessionTotal: 1_160_000));

    TestAssert.Equal(
        "上下文：110.9K/1.0M (10.6%)      缓存命中率：92%      输入：108K      输出：146      输出速度：33.5 tok/s",
        text);
    TestAssert.True(!text.Contains("会话累计", StringComparison.Ordinal));
});

Run("omits unavailable usage-line metrics without inventing zero", () =>
{
    var text = MetricsFormatter.Format(new MetricsSnapshot(
        ContextUsed: 330,
        ContextLimit: 1000,
        ContextPercent: 33,
        CacheHitPercent: null,
        LastInput: null,
        LastOutput: null,
        LastTotal: null,
        OutputTokensPerSecond: null,
        SessionTotal: null));

    TestAssert.Equal("上下文：330/1.0K (33%)", text);
});

Run("assigns Microsoft YaHei to labels and Times New Roman to values", () =>
{
    var segments = MetricsFormatter.FormatSegments(new MetricsSnapshot(
        ContextUsed: 110_900,
        ContextLimit: 1_048_576,
        ContextPercent: 10.6,
        CacheHitPercent: 92,
        LastInput: 108_000,
        LastOutput: 146,
        LastTotal: 110_900,
        OutputTokensPerSecond: 33.5,
        SessionTotal: 1_160_000));

    TestAssert.True(segments.Count > 0);
    TestAssert.Equal("上下文：", segments[0].Text);
    TestAssert.True(segments[0].FontRole == UsageFontRole.ChineseLabel);
    var contextValue = segments.First(segment => segment.Text.StartsWith("110.9K", StringComparison.Ordinal));
    TestAssert.True(contextValue.FontRole == UsageFontRole.LatinValue);
    TestAssert.True(segments.All(segment =>
        segment.Text.Any(character => character is >= '\u4e00' and <= '\u9fff')
            ? segment.FontRole == UsageFontRole.ChineseLabel
            : true));
});

Run("does not include session total in usage segments", () =>
{
    var text = MetricsFormatter.Format(new MetricsSnapshot(
        ContextUsed: 330,
        ContextLimit: 1000,
        ContextPercent: 33,
        CacheHitPercent: 50,
        LastInput: 100,
        LastOutput: 10,
        LastTotal: 110,
        OutputTokensPerSecond: 5,
        SessionTotal: 10_000_000));

    TestAssert.True(!text.Contains("会话累计", StringComparison.Ordinal));
});

Run("recognizes Microsoft Store and standalone Codex executables", () =>
{
    TestAssert.True(CodexWindowMatcher.IsCodexExecutablePath(
        @"C:\Program Files\WindowsApps\OpenAI.Codex_26.1021.3705.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe"));
    TestAssert.True(CodexWindowMatcher.IsCodexExecutablePath(
        @"C:\Users\Example\AppData\Local\OpenAI\Codex\app\ChatGPT.exe"));
    TestAssert.True(CodexWindowMatcher.IsCodexWindowTitle("ChatGPT"));
    TestAssert.True(CodexWindowMatcher.IsCodexWindowTitle("Project - Codex"));
});

Run("rejects unrelated ChatGPT executables and titles", () =>
{
    TestAssert.True(!CodexWindowMatcher.IsCodexExecutablePath(
        @"C:\Users\Example\AppData\Local\OtherVendor\ChatGPT.exe"));
    TestAssert.True(!CodexWindowMatcher.IsCodexWindowTitle("ChatGPT Help"));
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

    public static void Equal(string expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected \"{expected}\", got \"{actual ?? "null"}\".");
        }
    }

    public static void NotNull(object? actual)
    {
        if (actual is null)
        {
            throw new InvalidOperationException("Expected a value, got null.");
        }
    }

    public static void True(bool actual)
    {
        if (!actual)
        {
            throw new InvalidOperationException("Expected true, got false.");
        }
    }
}

sealed class TestDirectory : IDisposable
{
    private TestDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TestDirectory Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "codex-live-usage-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TestDirectory(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
