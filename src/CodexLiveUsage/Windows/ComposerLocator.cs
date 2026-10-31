using System.Windows.Automation;
using CodexLiveUsage.Core;

namespace CodexLiveUsage.Windows;

internal static class ComposerLocator
{
    public static IReadOnlyList<PixelRect> InspectAncestors(CodexWindowInfo window)
    {
        var root = AutomationElement.FromHandle(window.Handle);
        var edits = root.FindAll(TreeScope.Descendants, new PropertyCondition(
            AutomationElement.ControlTypeProperty, ControlType.Edit));
        var edit = edits.Cast<AutomationElement>()
            .Where(e => ToBoundingRect(e) is { Width: > 100, Height: > 24 } b &&
                b.Top > window.Bounds.Top + window.Bounds.Height / 2)
            .OrderByDescending(e => ToBoundingRect(e)!.Value.Bottom).FirstOrDefault();
        var result = new List<PixelRect>();
        for (var depth = 0; edit is not null && depth < 14; depth++)
        {
            if (ToBoundingRect(edit) is { } bounds) result.Add(bounds);
            edit = TreeWalker.RawViewWalker.GetParent(edit);
        }
        return result;
    }

    public static PixelRect? Find(CodexWindowInfo window)
    {
        try
        {
            var root = AutomationElement.FromHandle(window.Handle);
            var condition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                System.Windows.Automation.ControlType.Edit);
            var edits = root.FindAll(TreeScope.Descendants, condition);

            var edit = edits
                .Cast<AutomationElement>()
                .Select(element => new { Element = element, Bounds = ToBoundingRect(element) })
                .Where(item => item is not null)
                .Where(item => item!.Bounds is not null)
                .Select(item => item!)
                .Where(item => item.Bounds!.Value.Width > 100 * window.DpiScale && item.Bounds.Value.Height > 16 * window.DpiScale)
                .Where(item => item.Bounds!.Value.Top > window.Bounds.Top + window.Bounds.Height / 2)
                .Where(item => item.Bounds!.Value.Bottom <= window.Bounds.Bottom)
                .OrderByDescending(item => item.Bounds!.Value.Bottom)
                .ThenByDescending(item => item.Bounds!.Value.Width)
                .FirstOrDefault();

            if (edit?.Bounds is null)
            {
                return null;
            }

            return FindComposerContainer(edit.Element, edit.Bounds.Value, window.Bounds, window.DpiScale);
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

    private static PixelRect? ToBoundingRect(AutomationElement element)
    {
        if (element.Current.IsOffscreen)
        {
            return null;
        }

        var bounds = element.Current.BoundingRectangle;
        if (bounds.IsEmpty ||
            !double.IsFinite(bounds.Left) ||
            !double.IsFinite(bounds.Top) ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
        {
            return null;
        }

        return new PixelRect(
            (int)Math.Round(bounds.Left),
            (int)Math.Round(bounds.Top),
            Math.Max(1, (int)Math.Round(bounds.Width)),
            Math.Max(1, (int)Math.Round(bounds.Height)));
    }

    private static PixelRect? FindComposerContainer(
        AutomationElement edit,
        PixelRect editBounds,
        PixelRect windowBounds,
        double dpiScale)
    {
        var walker = TreeWalker.RawViewWalker;
        var current = edit;
        var ancestors = new List<PixelRect>();

        for (var depth = 0; depth < 14; depth++)
        {
            current = walker.GetParent(current);
            if (current is null)
            {
                break;
            }

            var bounds = ToBoundingRect(current);
            if (bounds is not null)
            {
                ancestors.Add(bounds.Value);
            }
        }

        return ComposerGeometry.FindContainer(editBounds, windowBounds, ancestors, dpiScale);
    }
}
