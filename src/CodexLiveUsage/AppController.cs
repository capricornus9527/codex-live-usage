using System.IO;
using System.Windows.Threading;
using CodexLiveUsage.Core;
using CodexLiveUsage.Windows;

namespace CodexLiveUsage;

internal sealed class AppController : IDisposable
{
    private static readonly UsageLineStyle[] LineStyles =
    [
        new(FontSize: 12, LineHeight: 13.5, Gap: 2),
        new(FontSize: 11, LineHeight: 12.5, Gap: 1),
        new(FontSize: 10, LineHeight: 11.5, Gap: 1),
        new(FontSize: 9, LineHeight: 10.5, Gap: 0),
    ];

    private readonly RolloutMetricsMonitor _metricsMonitor;
    private readonly CodexIpcActiveThreadMonitor _activeThreadMonitor;
    private readonly DispatcherTimer _timer;
    private UsageLineWindow? _line;
    private MetricsSnapshot? _snapshot;
    private IReadOnlyList<UsageSegment>? _segments;
    private DateTime _nextMetricsPollUtc = DateTime.MinValue;
    private string? _observedThreadId;
    private string? _lastConnectedThreadId;
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
        _activeThreadMonitor = new CodexIpcActiveThreadMonitor();
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
        _activeThreadMonitor.Dispose();
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
            var route = _activeThreadMonitor.GetStatus();
            if (route.IsConnected && !string.IsNullOrWhiteSpace(route.ThreadId))
            {
                _lastConnectedThreadId = route.ThreadId;
            }
            else if (!route.IsConnected)
            {
                _lastConnectedThreadId = null;
            }

            var desiredThreadId = route.IsConnected
                ? route.ThreadId ?? _lastConnectedThreadId
                : null;
            if (!string.Equals(_observedThreadId, desiredThreadId, StringComparison.OrdinalIgnoreCase))
            {
                _observedThreadId = desiredThreadId;
                _metricsMonitor.SelectThread(desiredThreadId);
                _nextMetricsPollUtc = DateTime.MinValue;
            }

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
                1,
                composer.Value.Width / window.DpiScale - 8);
            PixelRect? placement = null;
            foreach (var style in LineStyles)
            {
                var fitted = _line.RenderFitting(_snapshot!, maximumWidthDip, style);
                if (fitted is null) break;
                var sizeDip = fitted.Value;
                var lineSize = new PixelSize(
                    Math.Max(1, (int)Math.Ceiling(sizeDip.Width * window.DpiScale)),
                    Math.Max(1, (int)Math.Ceiling(sizeDip.Height * window.DpiScale)));
                placement = OverlayPlacement.BelowComposer(
                    window.Bounds,
                    composer.Value,
                    lineSize,
                    style.Gap);
                if (placement is not null)
                {
                    break;
                }
            }
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
