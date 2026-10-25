using System.Globalization;

namespace CodexLiveUsage.Core;

public static class MetricsFormatter
{
    public static string Format(MetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var parts = new List<string>(6);

        if (snapshot.ContextUsed is not null && snapshot.ContextLimit is not null)
        {
            var percent = snapshot.ContextPercent ??
                Math.Clamp(snapshot.ContextUsed.Value / snapshot.ContextLimit.Value * 100, 0, 100);
            parts.Add(
                $"上下文 {CompactCount(snapshot.ContextUsed.Value)}/{CompactLimit(snapshot.ContextLimit.Value)} ({FormatNumber(percent)}%)");
        }

        if (snapshot.CacheHitPercent is not null)
        {
            parts.Add($"缓存命中率 {FormatNumber(snapshot.CacheHitPercent.Value)}%");
        }

        if (snapshot.LastInput is not null)
        {
            parts.Add($"输入 {CompactCount(snapshot.LastInput.Value)}");
        }

        if (snapshot.LastOutput is not null)
        {
            parts.Add($"输出 {CompactCount(snapshot.LastOutput.Value)}");
        }

        if (snapshot.OutputTokensPerSecond is not null)
        {
            parts.Add($"输出速度 {FormatNumber(snapshot.OutputTokensPerSecond.Value)} tok/s");
        }

        if (snapshot.SessionTotal is not null)
        {
            parts.Add($"会话累计 {CompactCount(snapshot.SessionTotal.Value)}");
        }

        return string.Join(" · ", parts);
    }

    private static string CompactCount(double value)
    {
        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000_000)
        {
            return $"{Trim(absolute / 1_000_000_000, "0.##")}B";
        }

        if (absolute >= 1_000_000)
        {
            return $"{Trim(absolute / 1_000_000, "0.##")}M";
        }

        if (absolute >= 1_000)
        {
            return $"{Trim(absolute / 1_000, "0.#")}K";
        }

        return FormatNumber(value);
    }

    private static string CompactLimit(double value)
    {
        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000_000)
        {
            return $"{Trim(absolute / 1_000_000_000, "0.0")}B";
        }

        if (absolute >= 1_000_000)
        {
            return $"{Trim(absolute / 1_000_000, "0.0")}M";
        }

        if (absolute >= 1_000)
        {
            return $"{Trim(absolute / 1_000, "0.0")}K";
        }

        return FormatNumber(value);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string Trim(double value, string format)
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
