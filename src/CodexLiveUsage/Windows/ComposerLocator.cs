using System.Windows.Automation;
using CodexLiveUsage.Core;

namespace CodexLiveUsage.Windows;

internal static class ComposerLocator
{
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
                .Where(item => item.Bounds!.Value.Width > 300 && item.Bounds.Value.Height > 24)
                .Where(item => item.Bounds!.Value.Top > window.Bounds.Top + window.Bounds.Height / 2)
                .Where(item => item.Bounds!.Value.Bottom <= window.Bounds.Bottom)
                .OrderByDescending(item => item.Bounds!.Value.Bottom)
                .ThenByDescending(item => item.Bounds!.Value.Width)
                .FirstOrDefault();

            if (edit?.Bounds is null)
            {
                return null;
            }

            return FindComposerContainer(edit.Element, edit.Bounds.Value, window.Bounds);
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

    private static PixelRect FindComposerContainer(
        AutomationElement edit,
        PixelRect editBounds,
        PixelRect windowBounds)
    {
        var walker = TreeWalker.RawViewWalker;
        var current = edit;
        PixelRect? best = null;
        var maximumWidth = editBounds.Width + Math.Max(80, editBounds.Height * 2);

        for (var depth = 0; depth < 8; depth++)
        {
            current = walker.GetParent(current);
            if (current is null)
            {
                break;
            }

            var bounds = ToBoundingRect(current);
            if (bounds is null ||
                bounds.Value.Width > maximumWidth ||
                bounds.Value.Height <= editBounds.Height ||
                bounds.Value.Height > editBounds.Height * 5 ||
                bounds.Value.Left > windowBounds.Right ||
                bounds.Value.Right < windowBounds.Left)
            {
                continue;
            }

            if (best is null ||
                bounds.Value.Height > best.Value.Height ||
                (bounds.Value.Height == best.Value.Height && bounds.Value.Width < best.Value.Width))
            {
                best = bounds;
            }
        }

        return best ?? editBounds;
    }
}
