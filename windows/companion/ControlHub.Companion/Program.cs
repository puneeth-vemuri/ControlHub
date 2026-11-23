// ControlHub Companion — runs in user interactive session at sign-in.
// Connects to the ControlHub Windows Service via named pipe.
// Receives commands and executes user-session operations (lock, etc.).
// M1: Lock workstation only. M4+ will add screen capture, tray icon, sharing indicator.

using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using ControlHub.IpcContracts;

namespace ControlHub.Companion;

internal static class Program
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    private static readonly CancellationTokenSource _cts = new();

    static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cts.Cancel(); };

        Console.WriteLine("ControlHub Companion starting...");
        await RunWithReconnectAsync(_cts.Token);
        Console.WriteLine("ControlHub Companion stopped.");
    }

    private static async Task RunWithReconnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndProcessAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Companion error: {ex.Message}");
            }
            if (!ct.IsCancellationRequested)
            {
                Console.WriteLine("Reconnecting in 5 seconds...");
                await Task.Delay(5000, ct).ContinueWith(_ => { }); // swallow cancellation
            }
        }
    }

    private static async Task ConnectAndProcessAsync(CancellationToken ct)
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            PipeNames.CompanionPipe,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        Console.WriteLine($"Connecting to pipe: {PipeNames.CompanionPipe}...");
        await pipe.ConnectAsync(5000, ct);  // 5-second timeout
        Console.WriteLine("Connected to ControlHub Service.");

        using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

        // Send initial status
        var statusMsg = new StatusMessage(
            companionOnline: true,
            sessionUser: Environment.UserName,
            ts: DateTimeOffset.UtcNow.ToString("O")
        );
        await writer.WriteLineAsync(IpcSerializer.Serialize(statusMsg).TrimEnd('\n'));
        Console.WriteLine($"Sent status: online, user={Environment.UserName}");

        // Start periodic heartbeat
        var heartbeatTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(10_000, ct);
                var hb = new StatusMessage(true, Environment.UserName, DateTimeOffset.UtcNow.ToString("O"));
                try { await writer.WriteLineAsync(IpcSerializer.Serialize(hb).TrimEnd('\n')); }
                catch { break; }
            }
        }, ct);

        // Process incoming commands
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            var msg = IpcSerializer.DeserializeBase(line);
            if (msg?.Type != "cmd") continue;

            var cmd = IpcSerializer.Deserialize<CommandMessage>(line);
            if (cmd == null) continue;

            Console.WriteLine($"Received command: {cmd.Cmd}");
            ResponseMessage resp = ExecuteCommand(cmd);
            await writer.WriteLineAsync(IpcSerializer.Serialize(resp).TrimEnd('\n'));
        }

        await heartbeatTask;
    }

    private static ResponseMessage ExecuteCommand(CommandMessage cmd)
    {
        return cmd.Cmd switch
        {
            "ping" => new ResponseMessage(cmd.ReqId ?? "", true),
            "lock" => ExecuteLock(cmd),
            _ => new ResponseMessage(cmd.ReqId ?? "", false, "UNKNOWN_COMMAND")
        };
    }

    private static ResponseMessage ExecuteLock(CommandMessage cmd)
    {
        // LockWorkStation() must run in the interactive user session.
        // The companion runs at user logon in the interactive session, so this is correct.
        Console.WriteLine("Executing LockWorkStation()...");
        bool ok = LockWorkStation();
        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"LockWorkStation failed: Win32 error {err}");
            return new ResponseMessage(cmd.ReqId ?? "", false, $"LOCK_FAILED_{err}");
        }
        Console.WriteLine("LockWorkStation() succeeded.");
        return new ResponseMessage(cmd.ReqId ?? "", true);
    }
}
