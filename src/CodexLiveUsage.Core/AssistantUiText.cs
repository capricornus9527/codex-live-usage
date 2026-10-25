using System.Text.RegularExpressions;

namespace CodexLiveUsage.Core;

public static partial class AssistantUiText
{
    public static bool IsTransientStatus(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (trimmed is "正在思考" or "最新一条回复" or "Thinking")
        {
            return true;
        }

        return FileCountStatus().IsMatch(trimmed);
    }

    [GeneratedRegex(
        @"^(?:\d[\d,]*\s*个文件已更改|\d[\d,]*\s+files?\s+changed)(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FileCountStatus();
}
