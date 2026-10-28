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
    private static readonly FontFamily ChineseFont = new("Microsoft YaHei UI");
    private static readonly FontFamily LatinFont = new("Times New Roman");
    private static readonly Brush LightTextBrush =
        FrozenBrush(Color.FromRgb(167, 168, 169));
    private static readonly Brush DarkTextBrush =
        FrozenBrush(Color.FromRgb(178, 184, 188));
    private readonly TextBlock _textBlock;
    private readonly Dictionary<UsageLineStyle, Size> _measuredSizes = [];
    private readonly List<Run> _runs = [];
    private readonly List<UsageFontRole> _runRoles = [];
    private string _contentSignature = string.Empty;
    private double _measuredMaxWidth = double.NaN;
    private bool? _lightTheme;
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
            FontFamily = ChineseFont,
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            LineHeight = 13.5,
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

    public Size Render(
        IReadOnlyList<UsageSegment> segments,
        double maximumWidthDip,
        UsageLineStyle style)
    {
        var maxWidth = Math.Max(80, maximumWidthDip);
        var signature = string.Concat(
            segments.Select(segment => $"{segment.FontRole}:{segment.Text}\u001f"));
        var themeChanged = ApplyTheme();
        var contentChanged = !string.Equals(signature, _contentSignature, StringComparison.Ordinal);
        if (contentChanged)
        {
            UpdateInlines(segments);
            _contentSignature = signature;
        }

        if (contentChanged ||
            themeChanged ||
            !maxWidth.Equals(_measuredMaxWidth))
        {
            _textBlock.MaxWidth = maxWidth;
            _measuredMaxWidth = maxWidth;
            _measuredSizes.Clear();
        }

        if (_measuredSizes.TryGetValue(style, out var cached))
        {
            Width = cached.Width;
            Height = cached.Height;
            return cached;
        }

        _textBlock.FontSize = style.FontSize;
        _textBlock.LineHeight = style.LineHeight;
        _textBlock.Measure(new Size(maxWidth, double.PositiveInfinity));
        var desired = _textBlock.DesiredSize;
        Width = Math.Ceiling(Math.Max(120, desired.Width));
        Height = Math.Ceiling(Math.Max(style.FontSize, desired.Height));
        var measured = new Size(Width, Height);
        _measuredSizes[style] = measured;
        return measured;
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

    private bool ApplyTheme()
    {
        var light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1) is not int value || value != 0;
        if (_lightTheme == light)
        {
            return false;
        }

        _lightTheme = light;
        _textBlock.Foreground = light ? LightTextBrush : DarkTextBrush;
        return true;
    }

    private void UpdateInlines(IReadOnlyList<UsageSegment> segments)
    {
        var canReuse = _runs.Count == segments.Count &&
            _runRoles.SequenceEqual(segments.Select(segment => segment.FontRole));
        if (!canReuse)
        {
            _textBlock.Inlines.Clear();
            _runs.Clear();
            _runRoles.Clear();
            foreach (var segment in segments)
            {
                var run = new Run(segment.Text)
                {
                    FontFamily = segment.FontRole == UsageFontRole.ChineseLabel
                        ? ChineseFont
                        : LatinFont,
                };
                _runs.Add(run);
                _runRoles.Add(segment.FontRole);
                _textBlock.Inlines.Add(run);
            }
            return;
        }

        for (var index = 0; index < segments.Count; index++)
        {
            _runs[index].Text = segments[index].Text;
        }
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

internal sealed record UsageLineStyle(double FontSize, double LineHeight, int Gap);
