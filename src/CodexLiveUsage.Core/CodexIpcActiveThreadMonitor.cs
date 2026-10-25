// IPC framing and active-thread routing are adapted from
// soleillevant0125/codex-token-overlay (MIT). See licenses/soleillevant0125-MIT.txt.
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;

namespace CodexLiveUsage.Core;

public sealed record ActiveThreadStatus(
    string? ThreadId,
    int ActiveWindowCount,
    bool IsConnected,
    long Version,
    string? LastError,
    IReadOnlyList<string> RecentEvents);

public sealed class CodexIpcActiveThreadMonitor : IDisposable
{
    private const string PipeName = "codex-ipc";
    private const int MaximumWireFrameBytes = 256 * 1024 * 1024;
    private const int MaximumJsonFrameBytes = 4 * 1024 * 1024;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ActiveThreadRouter _router = new();
    private readonly Queue<string> _recentEvents = new();
    private readonly Task _runner;
    private bool _isConnected;
    private long _version;
    private string? _lastError;
    private bool _disposed;

    public CodexIpcActiveThreadMonitor()
    {
        _runner = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public ActiveThreadStatus GetStatus()
    {
        lock (_sync)
        {
            return new ActiveThreadStatus(
                _router.CurrentThreadId,
                _router.ActiveWindowCount,
                _isConnected,
                _version,
                _lastError,
                _recentEvents.ToArray());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        try
        {
            _runner.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The monitor is best-effort and has no user-facing shutdown failure.
        }
        _cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var retryDelay = TimeSpan.FromMilliseconds(350);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2500, cancellationToken).ConfigureAwait(false);
                MarkConnected();
                retryDelay = TimeSpan.FromMilliseconds(350);
                await SendInitializeAsync(pipe, cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
                {
                    var prefix = new byte[sizeof(uint)];
                    if (!await ReadExactlyAsync(pipe, prefix, cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }

                    var frameLength = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
                    if (frameLength == 0 || frameLength > MaximumWireFrameBytes)
                    {
                        throw new InvalidDataException($"Invalid Codex IPC frame length: {frameLength}");
                    }

                    if (frameLength > MaximumJsonFrameBytes)
                    {
                        await DrainExactlyAsync(pipe, frameLength, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var payload = new byte[frameLength];
                    if (!await ReadExactlyAsync(pipe, payload, cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }

                    try
                    {
                        ProcessFrame(payload);
                    }
                    catch (Exception error) when (
                        error is JsonException or InvalidOperationException)
                    {
                        // Unknown malformed broadcasts do not invalidate a healthy route.
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error) when (
                error is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                TimeoutException)
            {
                MarkDisconnected(error.Message);
            }

            MarkDisconnected(null);
            try
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            retryDelay = TimeSpan.FromMilliseconds(
                Math.Min(5000, retryDelay.TotalMilliseconds * 2));
        }
    }

    private static async Task SendInitializeAsync(
        Stream pipe,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            type = "request",
            requestId = Guid.NewGuid().ToString(),
            sourceClientId = "initializing-client",
            version = 0,
            method = "initialize",
            @params = new { clientType = "codex-live-usage" },
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(request);
        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)payload.Length);
        await pipe.WriteAsync(prefix.AsMemory(), cancellationToken).ConfigureAwait(false);
        await pipe.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ProcessFrame(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var type) ||
            type.GetString() != "broadcast" ||
            !root.TryGetProperty("method", out var method) ||
            !root.TryGetProperty("params", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        RecordEvent(method.GetString(), parameters);

        if (method.GetString() == "client-status-changed")
        {
            if (parameters.TryGetProperty("status", out var status) &&
                status.GetString() == "disconnected" &&
                parameters.TryGetProperty("clientId", out var disconnectedClient) &&
                _router.ApplyClientDisconnected(disconnectedClient.GetString() ?? string.Empty))
            {
                IncrementVersion();
            }
            return;
        }

        if (method.GetString() != "thread-stream-following-changed" ||
            !parameters.TryGetProperty("conversationId", out var conversation) ||
            !parameters.TryGetProperty("hostId", out var host) ||
            !parameters.TryGetProperty("following", out var following) ||
            following.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !root.TryGetProperty("sourceClientId", out var sourceClient))
        {
            return;
        }

        if (_router.ApplyFollowing(
            sourceClient.GetString() ?? string.Empty,
            host.GetString() ?? string.Empty,
            conversation.GetString() ?? string.Empty,
            following.GetBoolean()))
        {
            IncrementVersion();
        }
    }

    private void MarkConnected()
    {
        _router.Reset();
        lock (_sync)
        {
            _isConnected = true;
            _lastError = null;
            _version++;
        }
    }

    private void RecordEvent(string? method, JsonElement parameters)
    {
        var conversationId = parameters.TryGetProperty("conversationId", out var conversation)
            ? conversation.GetString()
            : null;
        var following = parameters.TryGetProperty("following", out var followingElement) &&
            followingElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? followingElement.GetBoolean()
                : (bool?)null;
        var summary = $"{method ?? "unknown"}|{conversationId ?? "-"}|{following?.ToString() ?? "-"}";
        lock (_sync)
        {
            _recentEvents.Enqueue(summary);
            while (_recentEvents.Count > 20)
            {
                _recentEvents.Dequeue();
            }
        }
    }

    private void MarkDisconnected(string? error)
    {
        _router.Reset();
        lock (_sync)
        {
            var changed = _isConnected || !string.IsNullOrWhiteSpace(_lastError);
            _isConnected = false;
            _lastError = string.IsNullOrWhiteSpace(error) ? _lastError : error;
            if (changed)
            {
                _version++;
            }
        }
    }

    private void IncrementVersion()
    {
        lock (_sync)
        {
            _version++;
        }
    }

    private static async Task<bool> ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }
            offset += read;
        }
        return true;
    }

    private static async Task DrainExactlyAsync(
        Stream stream,
        uint byteCount,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var remaining = byteCount;
        while (remaining > 0)
        {
            var chunk = (int)Math.Min(buffer.Length, remaining);
            if (!await ReadExactlyAsync(stream, buffer.AsMemory(0, chunk), cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }
            remaining -= (uint)chunk;
        }
    }
}
