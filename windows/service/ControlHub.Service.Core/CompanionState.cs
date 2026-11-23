using System.Collections.Concurrent;
using ControlHub.IpcContracts;

namespace ControlHub.Service.Core;

/// <summary>
/// Thread-safe state tracking and multiplexed IPC request/response dispatcher
/// for the companion connection. All stream reading is done exclusively by
/// CompanionPipeWorker; callers awaiting command responses register a TaskCompletionSource.
/// </summary>
public sealed class CompanionState
{
    private readonly object _lock = new();
    private bool _online;
    private string _sessionUser = string.Empty;
    private DateTimeOffset _lastSeen = DateTimeOffset.MinValue;
    private Stream? _companionStream;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ResponseMessage>> _pendingRequests = new();

    public bool IsOnline
    {
        get { lock (_lock) return _online && (DateTimeOffset.UtcNow - _lastSeen).TotalSeconds < 30; }
    }

    public void MarkOnline(string user, Stream stream)
    {
        lock (_lock)
        {
            _online = true;
            _sessionUser = user;
            _lastSeen = DateTimeOffset.UtcNow;
            _companionStream = stream;
        }
    }

    public void MarkOffline()
    {
        lock (_lock)
        {
            _online = false;
            _companionStream = null;
        }

        // Cancel/fail any in-flight requests waiting for companion responses
        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var tcs))
            {
                tcs.TrySetResult(new ResponseMessage(kvp.Key, false, "COMPANION_OFFLINE"));
            }
        }
    }

    public void Heartbeat()
    {
        lock (_lock) { _lastSeen = DateTimeOffset.UtcNow; }
    }

    public Stream? GetCompanionStream()
    {
        lock (_lock) return _companionStream;
    }

    /// <summary>
    /// Dispatches a response received by the single pipe reader to the waiting caller.
    /// </summary>
    public bool TryDispatchResponse(ResponseMessage resp)
    {
        Heartbeat();
        if (!string.IsNullOrEmpty(resp.ReqId) && _pendingRequests.TryRemove(resp.ReqId, out var tcs))
        {
            return tcs.TrySetResult(resp);
        }
        return false;
    }

    /// <summary>
    /// Sends a command down the companion stream and awaits the response asynchronously
    /// without creating a secondary stream reader.
    /// </summary>
    public async Task<ResponseMessage> SendCommandAsync(CommandMessage cmd, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!IsOnline)
            return new ResponseMessage(cmd.ReqId ?? string.Empty, false, "COMPANION_OFFLINE");

        Stream? stream;
        lock (_lock) { stream = _companionStream; }

        if (stream == null)
            return new ResponseMessage(cmd.ReqId ?? string.Empty, false, "COMPANION_OFFLINE");

        var reqId = cmd.ReqId ?? Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[reqId] = tcs;

        try
        {
            var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            var serialized = IpcSerializer.Serialize(cmd).TrimEnd('\n');
            await writer.WriteLineAsync(serialized);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            using var reg = linkedCts.Token.Register(() =>
            {
                if (_pendingRequests.TryRemove(reqId, out var pendingTcs))
                {
                    pendingTcs.TrySetResult(new ResponseMessage(reqId, false, "COMPANION_TIMEOUT"));
                }
            });

            return await tcs.Task;
        }
        catch (Exception)
        {
            _pendingRequests.TryRemove(reqId, out _);
            MarkOffline();
            return new ResponseMessage(reqId, false, "COMPANION_ERROR");
        }
    }
}
