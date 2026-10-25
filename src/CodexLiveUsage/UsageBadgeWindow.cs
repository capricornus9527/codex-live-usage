using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;
using Microsoft.Win32;

namespace CodexLiveUsage;

internal sealed class UsageBadgeWindow : Window
{
    private static readonly Brush BadgeBackground = FrozenBrush(Color.FromArgb(20, 20, 184, 166));
    private static readonly Brush BadgeBorder = FrozenBrush(Color.FromArgb(77, 20, 184, 166));
    private readonly TextBlock _textBlock;
    private readonly Border _badge;
    private IntPtr _handle;
    private IntPtr _owner;

    public UsageBadgeWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        Topmost = false;
        Focusable = false;
        IsHitTestVisible = false;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        _textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"),
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            LineHeight = 16.2,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextOptions.SetTextFormattingMode(_textBlock, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(_textBlock, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(_textBlock, TextHintingMode.Fixed);
        Typography.SetNumeralAlignment(_textBlock, FontNumeralAlignment.Tabular);

        _badge = new Border
        {
            Background = BadgeBackground,
            BorderBrush = BadgeBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(9, 5, 9, 5),
            Opacity = 0.9,
            Child = _textBlock,
        };
        Content = _badge;

        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            NativeMethods.ConfigureOverlayWindow(_handle);
        };
    }

    public Size Render(string text, double maximumWidthDip)
    {
        ApplyTheme();
        _textBlock.MaxWidth = Math.Max(80, maximumWidthDip - 18);
        _textBlock.Text = text;
        _badge.Measure(new Size(Math.Max(80, maximumWidthDip), double.PositiveInfinity));
        var desired = _badge.DesiredSize;
        Width = Math.Ceiling(Math.Max(80, desired.Width));
        Height = Math.Ceiling(Math.Max(26, desired.Height));
        return new Size(Width, Height);
    }

    public void ShowAt(IntPtr owner, PixelRect bounds, double dpiScale)
    {
        if (_handle == IntPtr.Zero)
        {
            _handle = new WindowInteropHelper(this).EnsureHandle();
            NativeMethods.ConfigureOverlayWindow(_handle);
        }

        if (_owner != owner)
        {
            NativeMethods.SetOwner(_handle, owner);
            _owner = owner;
        }

        Left = bounds.Left / dpiScale;
        Top = bounds.Top / dpiScale;
        Width = bounds.Width / dpiScale;
        Height = bounds.Height / dpiScale;
        if (!IsVisible)
        {
            Show();
            NativeMethods.SetOwner(_handle, owner);
        }

        NativeMethods.PlaceWindow(_handle, bounds);
    }

    public void HideWithoutActivation()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    private void ApplyTheme()
    {
        var light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1) is not int value || value != 0;
        _textBlock.Foreground = FrozenBrush(light
            ? Colors.Black
            : Color.FromRgb(238, 245, 242));
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
