namespace CodexLiveUsage.Core;

public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public readonly record struct PixelSize(int Width, int Height);

public static class OverlayPlacement
{
    public static PixelRect? BelowAnchor(
        PixelRect host,
        PixelRect anchor,
        PixelSize badge,
        int gap)
    {
        if (host.Width <= 0 || host.Height <= 0 || badge.Width <= 0 || badge.Height <= 0 || gap < 0)
        {
            return null;
        }

        var width = Math.Min(host.Width, badge.Width);
        var height = Math.Min(host.Height, badge.Height);
        var left = Math.Clamp(anchor.Left, host.Left, host.Right - width);
        var requestedTop = checked(anchor.Bottom + gap);
        var top = Math.Clamp(requestedTop, host.Top, host.Bottom - height);
        return new PixelRect(left, top, width, height);
    }

    public static PixelRect? FallbackAboveComposer(
        PixelRect host,
        PixelRect composer,
        PixelSize badge,
        int gap)
    {
        if (host.Width <= 0 || host.Height <= 0 || badge.Width <= 0 || badge.Height <= 0 || gap < 0)
        {
            return null;
        }

        var width = Math.Min(host.Width, badge.Width);
        var height = Math.Min(host.Height, badge.Height);
        var left = Math.Clamp(composer.Left, host.Left, host.Right - width);
        var requestedTop = checked(composer.Top - gap - height);
        var top = Math.Clamp(requestedTop, host.Top, host.Bottom - height);
        return new PixelRect(left, top, width, height);
    }
}
