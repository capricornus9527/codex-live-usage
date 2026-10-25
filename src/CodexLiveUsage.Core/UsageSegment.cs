namespace CodexLiveUsage.Core;

public enum UsageFontRole
{
    ChineseLabel,
    LatinValue,
}

public sealed record UsageSegment(string Text, UsageFontRole FontRole);
