using CodexLiveUsage.Core;

namespace CodexLiveUsage.Windows;

internal sealed record CodexWindowInfo(IntPtr Handle, PixelRect Bounds, double DpiScale);

internal static class CodexWindowLocator
{
    public static CodexWindowInfo? Find()
    {
        var foreground = NativeMethods.GetForegroundWindowHandle();
        CodexWindowInfo? best = null;
        var bestScore = long.MinValue;

        foreach (var hwnd in NativeMethods.EnumerateTopLevelWindows())
        {
            if (!NativeMethods.IsVisible(hwnd) || NativeMethods.IsMinimized(hwnd))
            {
                continue;
            }

            var title = NativeMethods.GetWindowTitle(hwnd);
            if (!CodexWindowMatcher.IsCodexWindowTitle(title))
            {
                continue;
            }

            var processPath = NativeMethods.GetProcessPath(hwnd);
            if (!CodexWindowMatcher.IsCodexExecutablePath(processPath))
            {
                continue;
            }

            var bounds = NativeMethods.GetWindowBounds(hwnd);
            if (bounds is null || bounds.Value.Width < 500 || bounds.Value.Height < 400)
            {
                continue;
            }

            var score = (long)bounds.Value.Width * bounds.Value.Height;
            if (hwnd == foreground)
            {
                score += 1_000_000_000L;
            }

            if (score <= bestScore)
            {
                continue;
            }

            var dpi = NativeMethods.GetWindowDpi(hwnd);
            best = new CodexWindowInfo(hwnd, bounds.Value, dpi / 96d);
            bestScore = score;
        }

        return best;
    }
}
