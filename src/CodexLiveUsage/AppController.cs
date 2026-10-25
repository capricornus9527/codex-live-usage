using System.IO;
using System.Windows.Threading;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;

namespace CodexLiveUsage;

internal sealed class AppController : IDisposable
{
    private readonly RolloutMetricsMonitor _metricsMonitor;
    private readonly DispatcherTimer _timer;
    private UsageLineWindow? _line;
    private MetricsSnapshot? _snapshot;
    private IReadOnlyList<UsageSegment>? _segments;
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
        _line?.Close();
        _line = null;
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
                _segments = _snapshot is null ? null : MetricsFormatter.FormatSegments(_snapshot);
                _nextMetricsPollUtc = DateTime.UtcNow.AddSeconds(1);
            }

            var window = CodexWindowLocator.Find();
            var composer = window is null ? null : ComposerLocator.Find(window);
            if (window is null || composer is null || _segments is null || _segments.Count == 0)
            {
                _line?.HideWithoutActivation();
                return;
            }

            _line ??= new UsageLineWindow();
            var maximumWidthDip = Math.Max(
                160,
                composer.Value.Width / window.DpiScale - 8);
            var sizeDip = _line.Render(_segments, maximumWidthDip);
            var lineSize = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(sizeDip.Width * window.DpiScale)),
                Math.Max(1, (int)Math.Ceiling(sizeDip.Height * window.DpiScale)));
            var placement = OverlayPlacement.BelowComposer(
                window.Bounds,
                composer.Value,
                lineSize,
                2);
            if (placement is null)
            {
                _line.HideWithoutActivation();
                return;
            }

            _line.ShowAt(window.Handle, placement.Value, window.DpiScale);
        }
        catch (Exception error) when (
            error is InvalidOperationException or
            System.Runtime.InteropServices.COMException or
            System.Windows.Automation.ElementNotAvailableException)
        {
            _line?.HideWithoutActivation();
        }
    }
}
