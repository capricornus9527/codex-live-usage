using System.Text;

namespace CodexLiveUsage.Core;

public sealed class RolloutMetricsMonitor
{
    private const int RetainedEventLines = 3;
    private readonly string _sessionsRoot;
    private readonly List<string> _eventLines = [];
    private string? _preferredThreadId;
    private string? _rolloutPath;
    private long _offset;
    private MetricsSnapshot? _lastSnapshot;
    private bool _suppressSpeedForSelection;

    public RolloutMetricsMonitor(string codexHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codexHome);
        _sessionsRoot = Path.Combine(codexHome, "sessions");
    }

    public string? PreferredThreadId => _preferredThreadId;

    public void SelectThread(string? threadId)
    {
        var normalized = string.IsNullOrWhiteSpace(threadId) ? null : threadId.Trim();
        if (string.Equals(_preferredThreadId, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _preferredThreadId = normalized;
        Reset(null);
    }

    public MetricsSnapshot? Poll()
    {
        try
        {
            var rollout = _preferredThreadId is null
                ? FindNewestRollout()
                : FindThreadRollout(_preferredThreadId);
            if (rollout is null)
            {
                if (_preferredThreadId is not null)
                {
                    Reset(null);
                    _lastSnapshot = MetricsSnapshot.Empty;
                    return _lastSnapshot;
                }

                return null;
            }

            if (!string.Equals(rollout, _rolloutPath, StringComparison.OrdinalIgnoreCase))
            {
                Reset(rollout);
            }

            using var stream = new FileStream(
                rollout,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length < _offset)
            {
                Reset(rollout);
            }

            if (stream.Length == _offset)
            {
                return _lastSnapshot ?? (_preferredThreadId is null
                    ? null
                    : MetricsSnapshot.Empty);
            }

            stream.Seek(_offset, SeekOrigin.Begin);
            var remaining = checked((int)(stream.Length - _offset));
            var buffer = new byte[remaining];
            stream.ReadExactly(buffer);
            ConsumeCompleteLines(buffer);
            return _lastSnapshot ?? (_preferredThreadId is null
                ? null
                : MetricsSnapshot.Empty);
        }
        catch (IOException)
        {
            return _lastSnapshot;
        }
        catch (UnauthorizedAccessException)
        {
            return _lastSnapshot;
        }
    }

    private string? FindNewestRollout()
    {
        return EnumerateRollouts()
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.FullName, StringComparer.Ordinal)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private string? FindThreadRollout(string threadId)
    {
        return EnumerateRollouts()
            .Where(file =>
                file.Name.Contains(threadId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.FullName, StringComparer.Ordinal)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private IEnumerable<FileInfo> EnumerateRollouts()
    {
        if (!Directory.Exists(_sessionsRoot))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(_sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists);
    }

    private void ConsumeCompleteLines(byte[] buffer)
    {
        var lineStart = 0;
        for (var index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] != (byte)'\n')
            {
                continue;
            }

            var lineLength = index - lineStart;
            if (lineLength > 0 && buffer[index - 1] == (byte)'\r')
            {
                lineLength--;
            }

            if (lineLength > 0)
            {
                var line = Encoding.UTF8.GetString(buffer, lineStart, lineLength);
                if (line.Contains("\"token_count\"", StringComparison.Ordinal) &&
                    RolloutMetricsParser.Parse([line]) is not null)
                {
                    _eventLines.Add(line);
                    if (_eventLines.Count > RetainedEventLines)
                    {
                        _eventLines.RemoveAt(0);
                    }
                }
            }

            lineStart = index + 1;
        }

        _offset += lineStart;
        var parsed = _eventLines.Count > 0
            ? RolloutMetricsParser.Parse(_eventLines)
            : null;
        if (parsed is not null)
        {
            parsed = parsed with
            {
                OutputTokensPerSecond = _suppressSpeedForSelection
                    ? 0
                    : parsed.OutputTokensPerSecond ?? 0,
            };
            _suppressSpeedForSelection = false;
            _lastSnapshot = parsed;
        }
        else if (_preferredThreadId is not null)
        {
            _lastSnapshot = MetricsSnapshot.Empty;
        }
    }

    private void Reset(string? path)
    {
        _rolloutPath = path;
        _offset = 0;
        _eventLines.Clear();
        _lastSnapshot = null;
        _suppressSpeedForSelection = true;
    }
}
