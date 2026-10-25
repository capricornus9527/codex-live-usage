namespace CodexLiveUsage.Core;

public static class CodexWindowMatcher
{
    public static bool IsCodexExecutablePath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        return executablePath.Contains(
                @"\WindowsApps\OpenAI.Codex_",
                StringComparison.OrdinalIgnoreCase) ||
            executablePath.Contains(
                @"\OpenAI\Codex\",
                StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCodexWindowTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var trimmed = title.Trim();
        return trimmed.Equals("Codex", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(" - Codex", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(" - ChatGPT", StringComparison.OrdinalIgnoreCase);
    }
}
