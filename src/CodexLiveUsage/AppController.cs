using System.IO;
using System.Windows.Threading;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;

namespace CodexLiveUsage;

internal sealed class AppController : IDisposable
{
    private readonly RolloutMetricsMonitor _metricsMonitor;
    private readonly DispatcherTimer _timer;
    private UsageBadgeWindow? _badge;
    private MetricsSnapshot? _snapshot;
    private string? _text;
    private DateTime _nextMetricsPollUtc = DateTime.MinValue;
    private bool _disposed;

    public AppController()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");
        }

        _metricsMonitor = new RolloutMetricsMonitor(codexHome);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _timer.Tick += (_, _) => Refresh();
    }

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _badge?.Close();
        _badge = null;
    }

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (DateTime.UtcNow >= _nextMetricsPollUtc)
            {
                _snapshot = _metricsMonitor.Poll();
                _text = _snapshot is null ? null : MetricsFormatter.Format(_snapshot);
                _nextMetricsPollUtc = DateTime.UtcNow.AddSeconds(1);
            }

            var window = CodexWindowLocator.Find();
            var anchor = window is null ? null : AssistantAnchorFinder.Find(window);
            if (window is null || anchor is null || string.IsNullOrWhiteSpace(_text))
            {
                _badge?.HideWithoutActivation();
                return;
            }

            _badge ??= new UsageBadgeWindow();
            var maximumWidthDip = Math.Max(
                160,
                (window.Bounds.Right - anchor.TextBounds.Left) / window.DpiScale - 24);
            var sizeDip = _badge.Render(_text, maximumWidthDip);
            var badgeSize = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(sizeDip.Width * window.DpiScale)),
                Math.Max(1, (int)Math.Ceiling(sizeDip.Height * window.DpiScale)));
            var contentHeight = Math.Max(1, anchor.ComposerBounds.Top - window.Bounds.Top);
            var contentHost = new PixelRect(
                window.Bounds.Left,
                window.Bounds.Top,
                window.Bounds.Width,
                contentHeight);
            var placement =
                OverlayPlacement.BelowAnchor(contentHost, anchor.TextBounds, badgeSize, 6) ??
                OverlayPlacement.FallbackAboveComposer(
                    window.Bounds,
                    anchor.ComposerBounds,
                    badgeSize,
                    6);
            if (placement is null)
            {
                _badge.HideWithoutActivation();
                return;
            }

            _badge.ShowAt(window.Handle, placement.Value, window.DpiScale);
        }
        catch (Exception error) when (
            error is InvalidOperationException or
            System.Runtime.InteropServices.COMException or
            System.Windows.Automation.ElementNotAvailableException)
        {
            _badge?.HideWithoutActivation();
        }
    }
}
