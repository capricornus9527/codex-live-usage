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
    double? SessionTotal);
