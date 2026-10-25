namespace CodexLiveUsage.Core;

public sealed record MetricsSnapshot(
    double? ContextUsed,
    double? ContextLimit,
    double? ContextPercent,
    double? CacheHitPercent,
    double? LastInput,
    double? LastOutput,
    double? LastTotal,
    double? OutputTokensPerSecond,
    double? SessionTotal)
{
    public static MetricsSnapshot Empty { get; } = new(
        ContextUsed: 0,
        ContextLimit: null,
        ContextPercent: null,
        CacheHitPercent: 0,
        LastInput: 0,
        LastOutput: 0,
        LastTotal: 0,
        OutputTokensPerSecond: 0,
        SessionTotal: 0);
}
