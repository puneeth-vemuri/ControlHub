using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using ControlHub.IpcContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlHub.Service.Core;

/// <summary>
/// Hosts \\.\pipe\ControlHubService. Accepts commands from the FastAPI agent.
/// Handles machine-level power operations (sleep, hibernate) via <see cref="IPowerExecutor"/>
/// and routes desktop-level commands (lock) to the user-session companion.
/// </summary>
public sealed class ServicePipeWorker : BackgroundService
{
    private readonly ILogger<ServicePipeWorker> _log;
    private readonly CompanionState _companion;
    private readonly IPowerExecutor _power;
    private readonly string _pipeName;

    public ServicePipeWorker(
        ILogger<ServicePipeWorker> log,
        CompanionState companion,
        IPowerExecutor power,
        string? pipeName = null)
    {
        _log = log;
        _companion = companion;
        _power = power;
        _pipeName = pipeName ?? PipeNames.ServicePipe;
    }

    /// <summary>
    /// Registry key under HKLM\SYSTEM\CurrentControlSet\Services\ControlHubService\Parameters
    /// where install.ps1 provisions the OwnerSid string at install time.
    /// </summary>
    public const string ParametersRegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\ControlHubService\Parameters";
    public const string OwnerSidValueName = "OwnerSid";

    /// <summary>
    /// Retrieve the provisioned owner SID string from the service's registry configuration key.
    /// Falls back to null if unconfigured or unreadable.
    /// </summary>
    public static string? GetConfiguredOwnerSid()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(ParametersRegistryKeyPath);
            return key?.GetValue(OwnerSidValueName) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Build the PipeSecurity DACL for \\.\pipe\ControlHubService.
    /// Grants access only to:
    /// - Current process identity (service account or test runner)
    /// - Built-in Administrators
    /// - NT AUTHORITY\LocalService
    /// - Configured owner SID (dynamically resolved from registry or explicit parameter)
    ///
    /// Explicitly denies or omits Authenticated Users (S-1-5-11) and Everyone (S-1-1-0)
    /// to preserve the single-owner security boundary without hardcoding machine-specific SIDs.
    /// </summary>
    public static PipeSecurity BuildPipeSecurity(string? ownerSidString = null)
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

        // Read the owner SID dynamically from parameter or registry
        var targetOwnerSid = ownerSidString ?? GetConfiguredOwnerSid();
        if (!string.IsNullOrWhiteSpace(targetOwnerSid))
        {
            try
            {
                var ownerSid = new SecurityIdentifier(targetOwnerSid);
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    ownerSid,
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));
            }
            catch (ArgumentException)
            {
                // Invalid SID format in configuration
            }
        }

        return pipeSecurity;
    }

    /// <summary>
    /// Create a named pipe server with an ACL secured by <see cref="BuildPipeSecurity"/>.
    /// </summary>
    private static NamedPipeServerStream CreateSecuredPipeServer(string pipeName, int maxInstances)
    {
        var pipeSecurity = BuildPipeSecurity();

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("ServicePipeWorker starting, pipe: {Pipe}", _pipeName);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreateSecuredPipeServer(
                    _pipeName,
                    NamedPipeServerStream.MaxAllowedServerInstances);

                await pipe.WaitForConnectionAsync(ct);
                _ = Task.Run(() => HandleApiConnectionAsync(pipe, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "ServicePipe accept error");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task HandleApiConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var _ = pipe;
        try
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                var baseMsg = IpcSerializer.DeserializeBase(line);
                if (baseMsg?.Type != "cmd") continue;

                var cmd = IpcSerializer.Deserialize<CommandMessage>(line);
                if (cmd == null) continue;

                _log.LogInformation("Received command: {Cmd} req={ReqId}", cmd.Cmd, cmd.ReqId);

                ResponseMessage resp = cmd.Cmd switch
                {
                    "ping"            => new ResponseMessage(cmd.ReqId!, true),
                    "get_status"      => new ResponseMessage(cmd.ReqId!, true, null, new { companion_online = _companion.IsOnline }),
                    "sleep"           => ExecuteSleep(cmd),
                    "hibernate"       => ExecuteHibernate(cmd),
                    "shutdown"        => ExecuteShutdown(cmd),
                    "restart"         => ExecuteRestart(cmd),
                    "cancel_shutdown" => ExecuteCancelShutdown(cmd),
                    "lock"            => await ForwardLockAsync(cmd, ct),
                    _                 => new ResponseMessage(cmd.ReqId!, false, "UNKNOWN_COMMAND")
                };

                await writer.WriteLineAsync(IpcSerializer.Serialize(resp).TrimEnd('\n'));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "API client disconnected");
        }
    }

    private ResponseMessage ExecuteSleep(CommandMessage cmd)
    {
        _log.LogInformation("Executing Sleep via IPowerExecutor (bForce=false)");
        bool ok = _power.Sleep();
        if (!ok)
        {
            _log.LogError("Sleep failed");
            return new ResponseMessage(cmd.ReqId!, false, "SLEEP_FAILED");
        }
        return new ResponseMessage(cmd.ReqId!, true);
    }

    private ResponseMessage ExecuteHibernate(CommandMessage cmd)
    {
        _log.LogInformation("Executing Hibernate via IPowerExecutor (bForce=false)");
        bool ok = _power.Hibernate();
        if (!ok)
        {
            _log.LogError("Hibernate failed");
            return new ResponseMessage(cmd.ReqId!, false, "HIBERNATE_FAILED");
        }
        return new ResponseMessage(cmd.ReqId!, true);
    }

    private ResponseMessage ExecuteShutdown(CommandMessage cmd)
    {
        if (!cmd.DelaySeconds.HasValue || cmd.DelaySeconds.Value < 0 || cmd.DelaySeconds.Value > 3600)
        {
            _log.LogWarning("Shutdown rejected: invalid delay_seconds {Delay}", cmd.DelaySeconds);
            ServiceFileLog.LogWarning($"Shutdown rejected: invalid delay_seconds {cmd.DelaySeconds}");
            return new ResponseMessage(cmd.ReqId!, false, "INVALID_DELAY");
        }

        _log.LogInformation("Executing Shutdown via IPowerExecutor delay={Delay} reason={Reason}", cmd.DelaySeconds.Value, cmd.Reason);
        ServiceFileLog.LogInfo($"Executing Shutdown via IPowerExecutor delay={cmd.DelaySeconds.Value} reason={cmd.Reason}");
        bool ok = _power.Shutdown(cmd.DelaySeconds.Value, cmd.Reason ?? "ControlHub requested a power operation", out int win32Error);
        if (!ok)
        {
            _log.LogError("Shutdown failed in IPowerExecutor, Win32 error: {Win32Error}", win32Error);
            ServiceFileLog.LogError($"Shutdown failed in IPowerExecutor, Win32 error: {win32Error}");
            return new ResponseMessage(cmd.ReqId!, false, "OS_REQUEST_FAILED", data: new { win32_error = win32Error });
        }
        return new ResponseMessage(cmd.ReqId!, true);
    }

    private ResponseMessage ExecuteRestart(CommandMessage cmd)
    {
        if (!cmd.DelaySeconds.HasValue || cmd.DelaySeconds.Value < 0 || cmd.DelaySeconds.Value > 3600)
        {
            _log.LogWarning("Restart rejected: invalid delay_seconds {Delay}", cmd.DelaySeconds);
            ServiceFileLog.LogWarning($"Restart rejected: invalid delay_seconds {cmd.DelaySeconds}");
            return new ResponseMessage(cmd.ReqId!, false, "INVALID_DELAY");
        }

        _log.LogInformation("Executing Restart via IPowerExecutor delay={Delay} reason={Reason}", cmd.DelaySeconds.Value, cmd.Reason);
        ServiceFileLog.LogInfo($"Executing Restart via IPowerExecutor delay={cmd.DelaySeconds.Value} reason={cmd.Reason}");
        bool ok = _power.Restart(cmd.DelaySeconds.Value, cmd.Reason ?? "ControlHub requested a power operation", out int win32Error);
        if (!ok)
        {
            _log.LogError("Restart failed in IPowerExecutor, Win32 error: {Win32Error}", win32Error);
            ServiceFileLog.LogError($"Restart failed in IPowerExecutor, Win32 error: {win32Error}");
            return new ResponseMessage(cmd.ReqId!, false, "OS_REQUEST_FAILED", data: new { win32_error = win32Error });
        }
        return new ResponseMessage(cmd.ReqId!, true);
    }

    private ResponseMessage ExecuteCancelShutdown(CommandMessage cmd)
    {
        _log.LogInformation("Executing CancelShutdown via IPowerExecutor");
        ServiceFileLog.LogInfo("Executing CancelShutdown via IPowerExecutor");
        bool ok = _power.CancelShutdown(out int win32Error);
        if (!ok)
        {
            _log.LogError("CancelShutdown failed in IPowerExecutor, Win32 error: {Win32Error}", win32Error);
            ServiceFileLog.LogError($"CancelShutdown failed in IPowerExecutor, Win32 error: {win32Error}");
            return new ResponseMessage(cmd.ReqId!, false, "OS_REQUEST_FAILED", data: new { win32_error = win32Error });
        }
        return new ResponseMessage(cmd.ReqId!, true);
    }

    private async Task<ResponseMessage> ForwardLockAsync(CommandMessage cmd, CancellationToken ct)
    {
        _log.LogInformation("Forwarding lock command req={ReqId} via CompanionState", cmd.ReqId);
        var fwdCmd = new CommandMessage("lock", cmd.ReqId!);
        return await _companion.SendCommandAsync(fwdCmd, TimeSpan.FromSeconds(5), ct);
    }
}
