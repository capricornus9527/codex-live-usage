namespace CodexLiveUsage.Core;

public static class ComposerGeometry
{
    public static PixelRect? FindContainer(
        PixelRect edit, PixelRect host, IEnumerable<PixelRect> ancestors, double dpiScale)
    {
        if (edit.Width <= 0 || edit.Height <= 0 || host.Height <= 0 ||
            !double.IsFinite(dpiScale) || dpiScale <= 0)
        {
            return null;
        }

        // Attachment previews grow independently of the editable text area.
        // Compare widths in DIPs, and require the container to enclose the edit.
        return ancestors.Where(bounds =>
                bounds.Left <= edit.Left && bounds.Right >= edit.Right &&
                bounds.Top <= edit.Top && bounds.Bottom >= edit.Bottom &&
                bounds.Width <= edit.Width + 80 * dpiScale &&
                bounds.Height > edit.Height && bounds.Height <= host.Height * 0.75 &&
                bounds.Left >= host.Left && bounds.Right <= host.Right &&
                bounds.Top >= host.Top && bounds.Bottom <= host.Bottom)
            .OrderByDescending(bounds => bounds.Bottom)
            .ThenByDescending(bounds => bounds.Width)
            .ThenByDescending(bounds => bounds.Height)
            .Select(bounds => (PixelRect?)bounds).FirstOrDefault();
    }
}
