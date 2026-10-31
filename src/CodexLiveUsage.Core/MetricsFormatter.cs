using System.Globalization;

namespace CodexLiveUsage.Core;

public static class MetricsFormatter
{
    public static string Format(MetricsSnapshot snapshot)
    {
        return string.Concat(FormatSegments(snapshot).Select(segment => segment.Text));
    }

    public static IReadOnlyList<UsageSegment> FormatSegments(MetricsSnapshot snapshot, int hiddenGroups = 0)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var metrics = new List<(string Label, string Value)>(5);

        if (snapshot.ContextUsed is not null && hiddenGroups < 4)
        {
            if (snapshot.ContextLimit is not null)
            {
                var percent = snapshot.ContextPercent ??
                    Math.Clamp(snapshot.ContextUsed.Value / snapshot.ContextLimit.Value * 100, 0, 100);
                metrics.Add((
                    "上下文",
                    $"{CompactCount(snapshot.ContextUsed.Value)}/{CompactLimit(snapshot.ContextLimit.Value)} ({FormatNumber(percent)}%)"));
            }
            else
            {
                metrics.Add(("上下文", $"{CompactCount(snapshot.ContextUsed.Value)} tokens"));
            }
        }

        if (snapshot.CacheHitPercent is not null && hiddenGroups < 3)
        {
            metrics.Add(("缓存命中率", $"{FormatNumber(snapshot.CacheHitPercent.Value)}%"));
        }

        if (snapshot.LastInput is not null && hiddenGroups < 1)
        {
            metrics.Add(("输入", CompactCount(snapshot.LastInput.Value)));
        }

        if (snapshot.LastOutput is not null && hiddenGroups < 1)
        {
            metrics.Add(("输出", CompactCount(snapshot.LastOutput.Value)));
        }

        if (snapshot.OutputTokensPerSecond is not null && hiddenGroups < 2)
        {
            metrics.Add(("输出速度", $"{FormatNumber(snapshot.OutputTokensPerSecond.Value)} tok/s"));
        }

        var segments = new List<UsageSegment>(metrics.Count * 2 + Math.Max(0, metrics.Count - 1));
        for (var index = 0; index < metrics.Count; index++)
        {
            segments.Add(new UsageSegment($"{metrics[index].Label}：", UsageFontRole.ChineseLabel));
            segments.Add(new UsageSegment(metrics[index].Value, UsageFontRole.LatinValue));
            if (index < metrics.Count - 1)
            {
                segments.Add(new UsageSegment("        ", UsageFontRole.ChineseLabel));
            }
        }

        return segments;
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
