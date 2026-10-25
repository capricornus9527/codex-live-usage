namespace CodexLiveUsage.Core;

public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public readonly record struct PixelSize(int Width, int Height);

public static class OverlayPlacement
{
    public static PixelRect? BelowComposer(
        PixelRect host,
        PixelRect composer,
        PixelSize line,
        int gap)
    {
        if (host.Width <= 0 || host.Height <= 0 || line.Width <= 0 || line.Height <= 0 || gap < 0)
        {
            return null;
        }

        var width = Math.Min(host.Width, line.Width);
        var height = Math.Min(host.Height, line.Height);
        var centeredLeft = composer.Left + (composer.Width - width) / 2;
        var left = Math.Clamp(centeredLeft, host.Left, host.Right - width);
        var top = checked(composer.Bottom + gap);
        return top + height <= host.Bottom
            ? new PixelRect(left, top, width, height)
            : null;
    }
}
