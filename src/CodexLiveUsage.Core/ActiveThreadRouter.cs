namespace CodexLiveUsage.Core;

public sealed class ActiveThreadRouter
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ActiveConversation> _activeByWindow =
        new(StringComparer.Ordinal);
    private long _sequence;
    private string? _currentThreadId;

    public string? CurrentThreadId
    {
        get
        {
            lock (_sync)
            {
                return _currentThreadId;
            }
        }
    }

    public int ActiveWindowCount
    {
        get
        {
            lock (_sync)
            {
                return _activeByWindow.Count;
            }
        }
    }

    public bool ApplyFollowing(
        string sourceClientId,
        string hostId,
        string conversationId,
        bool following)
    {
        if (string.IsNullOrWhiteSpace(sourceClientId) ||
            string.IsNullOrWhiteSpace(hostId) ||
            string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        lock (_sync)
        {
            var key = $"{sourceClientId}\u001f{hostId}";
            var changed = false;
            if (following)
            {
                _activeByWindow[key] = new ActiveConversation(conversationId, ++_sequence);
                changed = true;
            }
            else if (_activeByWindow.TryGetValue(key, out var active) &&
                active.ThreadId.Equals(conversationId, StringComparison.OrdinalIgnoreCase))
            {
                _activeByWindow.Remove(key);
                changed = true;
            }

            return changed | RecomputeCurrentThread();
        }
    }

    public bool ApplyClientDisconnected(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }

        lock (_sync)
        {
            var prefix = $"{clientId}\u001f";
            var removed = false;
            foreach (var key in _activeByWindow.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray())
            {
                _activeByWindow.Remove(key);
                removed = true;
            }

            return removed | RecomputeCurrentThread();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _activeByWindow.Clear();
            _currentThreadId = null;
        }
    }

    private bool RecomputeCurrentThread()
    {
        var next = _activeByWindow.Values
            .MaxBy(item => item.Sequence)
            ?.ThreadId;
        if (string.Equals(_currentThreadId, next, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _currentThreadId = next;
        return true;
    }

    private sealed record ActiveConversation(string ThreadId, long Sequence);
}
