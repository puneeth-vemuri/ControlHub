using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using ControlHub.IpcContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlHub.Service.Core;

/// <summary>
/// Hosts \\.\pipe\ControlHubCompanion. Accepts connections from the user-session companion.
/// Tracks companion online/offline state and holds the stream for forwarding commands.
/// </summary>
public sealed class CompanionPipeWorker : BackgroundService
{
    private readonly ILogger<CompanionPipeWorker> _log;
    private readonly CompanionState _companion;

    public CompanionPipeWorker(ILogger<CompanionPipeWorker> log, CompanionState companion)
    {
        _log = log;
        _companion = companion;
    }

    private static NamedPipeServerStream CreateSecuredCompanionPipeServer()
    {
        var pipeSecurity = new PipeSecurity();

        var currentIdentity = WindowsIdentity.GetCurrent();
        if (currentIdentity.User != null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                currentIdentity.User,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
        }

        var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            adminsSid,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            localServiceSid,
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        // Allow any authenticated interactive user to connect (companion runs in user session)
        var authUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            authUsersSid,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeNames.CompanionPipe,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("CompanionPipeWorker starting, pipe: {Pipe}", PipeNames.CompanionPipe);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreateSecuredCompanionPipeServer();
                await pipe.WaitForConnectionAsync(ct);
                _ = Task.Run(() => HandleCompanionConnectionAsync(pipe, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "CompanionPipe accept error");
                await Task.Delay(1000, ct);
            }
        }
        _companion.MarkOffline();
    }

    private async Task HandleCompanionConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var _ = pipe;
        try
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                var msg = IpcSerializer.DeserializeBase(line);
                if (msg?.Type == "status")
                {
                    var status = IpcSerializer.Deserialize<StatusMessage>(line);
                    if (status != null)
                    {
                        _log.LogInformation("Companion online: user={User}", status.SessionUser);
                        _companion.MarkOnline(status.SessionUser, pipe);
                    }
                }
                else if (msg?.Type == "resp")
                {
                    var resp = IpcSerializer.Deserialize<ResponseMessage>(line);
                    if (resp != null)
                    {
                        bool dispatched = _companion.TryDispatchResponse(resp);
                        _log.LogInformation("Companion response dispatched for req={ReqId}: ok={Ok}, handled={Handled}", resp.ReqId, resp.Ok, dispatched);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Companion disconnected");
        }
        finally
        {
            _companion.MarkOffline();
        }
    }
}
