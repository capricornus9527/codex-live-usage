using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;
using Microsoft.Win32;

namespace CodexLiveUsage;

internal sealed class UsageLineWindow : Window
{
    private readonly TextBlock _textBlock;
    private IntPtr _handle;
    private IntPtr _owner;

    public UsageLineWindow()
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
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            LineHeight = 14,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 1,
        };
        TextOptions.SetTextFormattingMode(_textBlock, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(_textBlock, TextRenderingMode.Grayscale);
        TextOptions.SetTextHintingMode(_textBlock, TextHintingMode.Fixed);
        Typography.SetNumeralAlignment(_textBlock, FontNumeralAlignment.Tabular);
        Content = _textBlock;

        SourceInitialized += (_, _) =>
        {
            _handle = new WindowInteropHelper(this).Handle;
            NativeMethods.ConfigureOverlayWindow(_handle);
        };
    }

    public Size Render(IReadOnlyList<UsageSegment> segments, double maximumWidthDip)
    {
        ApplyTheme();
        _textBlock.MaxWidth = Math.Max(80, maximumWidthDip);
        _textBlock.Inlines.Clear();
        foreach (var segment in segments)
        {
            _textBlock.Inlines.Add(new Run(segment.Text)
            {
                FontFamily = segment.FontRole == UsageFontRole.ChineseLabel
                    ? new FontFamily("Microsoft YaHei UI")
                    : new FontFamily("Times New Roman"),
            });
        }

        _textBlock.Measure(new Size(Math.Max(80, maximumWidthDip), double.PositiveInfinity));
        var desired = _textBlock.DesiredSize;
        Width = Math.Ceiling(Math.Max(120, desired.Width));
        Height = Math.Ceiling(Math.Max(15, desired.Height));
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
            ? Color.FromRgb(167, 168, 169)
            : Color.FromRgb(178, 184, 188));
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
