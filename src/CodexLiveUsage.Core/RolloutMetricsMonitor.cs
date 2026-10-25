using System.Text;

namespace CodexLiveUsage.Core;

public sealed class RolloutMetricsMonitor
{
    private const int RetainedEventLines = 3;
    private readonly string _sessionsRoot;
    private readonly List<string> _eventLines = [];
    private string? _rolloutPath;
    private long _offset;
    private MetricsSnapshot? _lastSnapshot;

    public RolloutMetricsMonitor(string codexHome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codexHome);
        _sessionsRoot = Path.Combine(codexHome, "sessions");
    }

    public MetricsSnapshot? Poll()
    {
        try
        {
            var newest = FindNewestRollout();
            if (newest is null)
            {
                return null;
            }

            if (!string.Equals(newest, _rolloutPath, StringComparison.OrdinalIgnoreCase))
            {
                Reset(newest);
            }

            using var stream = new FileStream(
                newest,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length < _offset)
            {
                Reset(newest);
            }

            if (stream.Length == _offset)
            {
                return _lastSnapshot;
            }

            stream.Seek(_offset, SeekOrigin.Begin);
            var remaining = checked((int)(stream.Length - _offset));
            var buffer = new byte[remaining];
            stream.ReadExactly(buffer);

            ConsumeCompleteLines(buffer);
            return _lastSnapshot;
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
        if (!Directory.Exists(_sessionsRoot))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(_sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.FullName, StringComparer.Ordinal)
            .Select(file => file.FullName)
            .FirstOrDefault();
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
                    RolloutMetricsParser.Parse([line]) is { } snapshot)
                {
                    _eventLines.Add(line);
                    if (_eventLines.Count > RetainedEventLines)
                    {
                        _eventLines.RemoveAt(0);
                    }

                    _lastSnapshot = snapshot;
                }
            }

            lineStart = index + 1;
        }

        _offset += lineStart;
        if (_eventLines.Count > 0)
        {
            _lastSnapshot = RolloutMetricsParser.Parse(_eventLines) ?? _lastSnapshot;
        }
    }

    private void Reset(string? path)
    {
        _rolloutPath = path;
        _offset = 0;
        _eventLines.Clear();
        _lastSnapshot = null;
    }
}
