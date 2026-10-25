using System.Text.RegularExpressions;
using System.Windows.Automation;
using CodexLiveUsage.Core;

namespace CodexLiveUsage.Windows;

internal sealed record AssistantAnchor(PixelRect TextBounds, PixelRect ComposerBounds);

internal static partial class AssistantAnchorFinder
{
    public static AssistantAnchor? Find(CodexWindowInfo window)
    {
        try
        {
            var root = AutomationElement.FromHandle(window.Handle);
            var composer = FindComposer(root, window);
            if (composer is null)
            {
                return null;
            }

            var textElements = FindTextElements(root, window, composer.Value);
            if (textElements.Count == 0)
            {
                return null;
            }

            var marker = textElements
                .Where(item => AssistantMarker().IsMatch(item.Name))
                .OrderByDescending(item => item.Bounds.Top)
                .FirstOrDefault();

            if (marker is not null)
            {
                var following = textElements
                    .Where(item => item.Bounds.Top >= marker.Bounds.Top - 2)
                    .Where(item => item.Bounds.Left >= marker.Bounds.Left - 4)
                    .ToArray();
                if (following.Length > 0)
                {
                    var left = following
                        .Where(item => item.Bounds.Width > 50)
                        .Select(item => item.Bounds.Left)
                        .DefaultIfEmpty(marker.Bounds.Left)
                        .Min();
                    var bottom = following.Max(item => item.Bounds.Bottom);
                    var right = Math.Min(window.Bounds.Right, following.Max(item => item.Bounds.Right));
                    return new AssistantAnchor(
                        new PixelRect(left, marker.Bounds.Top, Math.Max(1, right - left), Math.Max(1, bottom - marker.Bounds.Top)),
                        composer.Value);
                }
            }

            var fallback = textElements
                .Where(item => item.Bounds.Width > 100)
                .OrderByDescending(item => item.Bounds.Bottom)
                .FirstOrDefault();
            return fallback is null ? null : new AssistantAnchor(fallback.Bounds, composer.Value);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static PixelRect? FindComposer(AutomationElement root, CodexWindowInfo window)
    {
        var condition = new PropertyCondition(
            AutomationElement.ControlTypeProperty,
            System.Windows.Automation.ControlType.Edit);
        var edits = root.FindAll(TreeScope.Descendants, condition);

        return edits
            .Cast<AutomationElement>()
            .Select(ToElementBounds)
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item => !item.IsOffscreen)
            .Where(item => item.Bounds.Width > 300 && item.Bounds.Height > 24)
            .Where(item => item.Bounds.Top > window.Bounds.Top + window.Bounds.Height / 2)
            .Where(item => item.Bounds.Bottom <= window.Bounds.Bottom)
            .OrderByDescending(item => item.Bounds.Bottom)
            .ThenByDescending(item => item.Bounds.Width)
            .Select(item => (PixelRect?)item.Bounds)
            .FirstOrDefault();
    }

    private static List<TextElementInfo> FindTextElements(
        AutomationElement root,
        CodexWindowInfo window,
        PixelRect composer)
    {
        var condition = new PropertyCondition(
            AutomationElement.ControlTypeProperty,
            System.Windows.Automation.ControlType.Text);
        var texts = root.FindAll(TreeScope.Descendants, condition);
        var result = new List<TextElementInfo>();
        var contentLeft = window.Bounds.Left + Math.Min(240, window.Bounds.Width / 5);

        foreach (AutomationElement element in texts)
        {
            if (element.Current.ProcessId == Environment.ProcessId ||
                AssistantUiText.IsTransientStatus(element.Current.Name))
            {
                continue;
            }

            var info = ToElementBounds(element);
            if (info is null ||
                info.IsOffscreen ||
                string.IsNullOrWhiteSpace(info.Name) ||
                info.Bounds.Left < contentLeft ||
                info.Bounds.Top < window.Bounds.Top ||
                info.Bounds.Bottom > composer.Top)
            {
                continue;
            }

            result.Add(info);
        }

        return result;
    }

    private static TextElementInfo? ToElementBounds(AutomationElement element)
    {
        var bounds = element.Current.BoundingRectangle;
        if (bounds.IsEmpty ||
            !double.IsFinite(bounds.Left) ||
            !double.IsFinite(bounds.Top) ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
        {
            return null;
        }

        return new TextElementInfo(
            element.Current.Name ?? string.Empty,
            element.Current.IsOffscreen,
            new PixelRect(
                (int)Math.Round(bounds.Left),
                (int)Math.Round(bounds.Top),
                Math.Max(1, (int)Math.Round(bounds.Width)),
                Math.Max(1, (int)Math.Round(bounds.Height))));
    }

    [GeneratedRegex(@"^(ChatGPT|Codex)\s*(说|說|said)\s*[:：]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AssistantMarker();

    private sealed record TextElementInfo(string Name, bool IsOffscreen, PixelRect Bounds);
}
