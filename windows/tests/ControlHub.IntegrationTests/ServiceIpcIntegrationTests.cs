using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using ControlHub.IpcContracts;
using ControlHub.Service.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlHub.IntegrationTests;

/// <summary>
/// Integration tests for the ServicePipeWorker IPC round-trip.
/// Each test instance creates a worker with a unique pipe name to avoid cross-test conflicts.
/// 
/// Pipe ACL design: CreateSecuredPipeServer grants ReadWrite to:
///   - currentIdentity.User (the test runner's Windows account)
///   - BuiltinAdministrators
///   - NT AUTHORITY\LocalService
/// Non-owner accounts are implicitly denied. This is a code-level assertion; end-to-end
/// multi-account ACL enforcement is documented as a manual verification item.
/// </summary>
public class ServiceIpcIntegrationTests : IAsyncLifetime
{
    private readonly CompanionState _companion;
    private readonly ServicePipeWorker _worker;
    private readonly CancellationTokenSource _cts;
    private readonly string _pipeName;

    public ServiceIpcIntegrationTests()
    {
        // Unique pipe per test instance — prevents cross-test name collisions
        _pipeName = $"ControlHubTest_{Guid.NewGuid():N}";
        _companion = new CompanionState();
        _worker = new ServicePipeWorker(
            NullLogger<ServicePipeWorker>.Instance,
            _companion,
            new NoOpPowerExecutor(),
            pipeName: _pipeName);
        _cts = new CancellationTokenSource();
    }

    public async Task InitializeAsync()
    {
        await _worker.StartAsync(_cts.Token);
        // Give the BackgroundService time to enter ExecuteAsync and begin WaitForConnectionAsync
        await Task.Delay(300);
    }

    public async Task DisposeAsync()
    {
        await _worker.StopAsync(CancellationToken.None);
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task<NamedPipeClientStream> ConnectAsync()
    {
        var clientPipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(3000);
        return clientPipe;
    }

    [Fact]
    public async Task Ping_Over_NamedPipe_Returns_Ok_True()
    {
        using var clientPipe = await ConnectAsync();
        Assert.True(clientPipe.IsConnected, "Client pipe should be connected");

        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("ping", "test-ping-req");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.Equal("test-ping-req", resp.ReqId);
        Assert.True(resp.Ok, $"Expected Ok=true, got error={resp.Error}");
    }

    [Fact]
    public async Task GetStatus_Over_NamedPipe_Returns_CompanionOffline_Initially()
    {
        using var clientPipe = await ConnectAsync();

        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("get_status", "test-status-req");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.Equal("test-status-req", resp.ReqId);
        Assert.True(resp.Ok, $"get_status should succeed, got error={resp.Error}");
        // companion_online should be false since no companion connected
        Assert.Contains("companion_online", respLine, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("false", respLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lock_When_Companion_Offline_Returns_COMPANION_OFFLINE()
    {
        // Companion is not connected — lock should fail gracefully
        using var clientPipe = await ConnectAsync();

        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("lock", "test-lock-req");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.False(resp.Ok);
        Assert.Equal("COMPANION_OFFLINE", resp.Error);
    }

    [Fact]
    public async Task UnknownCommand_Returns_UNKNOWN_COMMAND_Error()
    {
        using var clientPipe = await ConnectAsync();

        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("not_a_real_command", "test-unknown-req");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.False(resp.Ok);
        Assert.Equal("UNKNOWN_COMMAND", resp.Error);
    }

    [Fact]
    public void ServicePipe_Dacl_Contains_OwnerSid_And_Excludes_AuthenticatedUsers_And_Everyone()
    {
        // Test with an explicit test owner SID to verify dynamic configuration injection
        const string testOwnerSid = "S-1-5-21-1234567890-1234567890-1234567890-1001";
        var security = ServicePipeWorker.BuildPipeSecurity(ownerSidString: testOwnerSid);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

        var allowedSids = new HashSet<string>();
        foreach (PipeAccessRule rule in rules)
        {
            if (rule.AccessControlType == AccessControlType.Allow)
            {
                allowedSids.Add(rule.IdentityReference.Value);
            }
        }

        // 1. Assert configured owner user SID is explicitly granted access
        Assert.Contains(testOwnerSid, allowedSids);

        // 2. Assert AuthenticatedUserSid (S-1-5-11) is NOT granted access
        var authUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null).Value;
        Assert.DoesNotContain(authUsersSid, allowedSids);

        // 3. Assert Everyone (S-1-1-0) is NOT granted access
        var worldSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null).Value;
        Assert.DoesNotContain(worldSid, allowedSids);
    }

    [Fact]
    public async Task Companion_SingleReader_Handles_Heartbeat_And_Response_On_One_Connection()
    {
        // Set up a simulated companion connection stream using an in-memory duplex pipe (Stream pair)
        var companionToService = new MemoryStream();
        var serviceToCompanion = new MemoryStream();

        // Use a pair of linked streams or a duplex pipe stream
        // In .NET, System.IO.Pipelines or NamedPipeServerStream can be used.
        // Even simpler: test CompanionState directly with a duplex stream wrapper.
        var duplex = new DuplexTestStream();

        _companion.MarkOnline("testuser", duplex);
        Assert.True(_companion.IsOnline);

        // Simulate incoming status message (heartbeat)
        var statusMsg = new StatusMessage(true, "testuser", DateTimeOffset.UtcNow.ToString("O"));
        _companion.Heartbeat();

        // Dispatch a lock command asynchronously in the background
        var lockTask = Task.Run(async () =>
        {
            var cmd = new CommandMessage("lock", "req-single-reader-test");
            return await _companion.SendCommandAsync(cmd, TimeSpan.FromSeconds(5));
        });

        // Verify the command was written to duplex stream
        await Task.Delay(100);
        duplex.Position = 0;
        using var reader = new StreamReader(duplex, Encoding.UTF8, leaveOpen: true);
        var writtenLine = await reader.ReadLineAsync();
        Assert.NotNull(writtenLine);
        var writtenCmd = IpcSerializer.Deserialize<CommandMessage>(writtenLine);
        Assert.NotNull(writtenCmd);
        Assert.Equal("lock", writtenCmd.Cmd);
        Assert.Equal("req-single-reader-test", writtenCmd.ReqId);

        // Simulate the single reader in CompanionPipeWorker reading a heartbeat, then the response
        _companion.Heartbeat(); // Heartbeat received

        var respMsg = new ResponseMessage("req-single-reader-test", true);
        bool dispatched = _companion.TryDispatchResponse(respMsg);
        Assert.True(dispatched, "TryDispatchResponse should return true for pending request");

        var result = await lockTask;
        Assert.True(result.Ok);
        Assert.Equal("req-single-reader-test", result.ReqId);
    }

    [Fact]
    public void Shutdown_Command_Serialization_RoundTrip_Succeeds()
    {
        var cmd = new CommandMessage("shutdown", "req-shutdown-1", delaySeconds: 45, reason: "Nightly maintenance");
        var serialized = IpcSerializer.Serialize(cmd);
        Assert.Contains("\"cmd\":\"shutdown\"", serialized);
        Assert.Contains("\"delay_seconds\":45", serialized);
        Assert.Contains("\"reason\":\"Nightly maintenance\"", serialized);

        var deserialized = IpcSerializer.Deserialize<CommandMessage>(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal("shutdown", deserialized.Cmd);
        Assert.Equal("req-shutdown-1", deserialized.ReqId);
        Assert.Equal(45, deserialized.DelaySeconds);
        Assert.Equal("Nightly maintenance", deserialized.Reason);
    }

    [Fact]
    public void Restart_Command_Serialization_RoundTrip_Succeeds()
    {
        var cmd = new CommandMessage("restart", "req-restart-1", delaySeconds: 60, reason: "Kernel update");
        var serialized = IpcSerializer.Serialize(cmd);
        Assert.Contains("\"cmd\":\"restart\"", serialized);
        Assert.Contains("\"delay_seconds\":60", serialized);
        Assert.Contains("\"reason\":\"Kernel update\"", serialized);

        var deserialized = IpcSerializer.Deserialize<CommandMessage>(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal("restart", deserialized.Cmd);
        Assert.Equal("req-restart-1", deserialized.ReqId);
        Assert.Equal(60, deserialized.DelaySeconds);
        Assert.Equal("Kernel update", deserialized.Reason);
    }

    [Fact]
    public void CancelShutdown_Command_Serialization_RoundTrip_Succeeds()
    {
        var cmd = new CommandMessage("cancel_shutdown", "req-cancel-1");
        var serialized = IpcSerializer.Serialize(cmd);
        Assert.Contains("\"cmd\":\"cancel_shutdown\"", serialized);
        Assert.DoesNotContain("delay_seconds", serialized);

        var deserialized = IpcSerializer.Deserialize<CommandMessage>(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal("cancel_shutdown", deserialized.Cmd);
        Assert.Equal("req-cancel-1", deserialized.ReqId);
        Assert.Null(deserialized.DelaySeconds);
        Assert.Null(deserialized.Reason);
    }
    [Fact]
    public async Task Shutdown_ValidDelay_Dispatches_Successfully()
    {
        using var clientPipe = await ConnectAsync();
        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("shutdown", "test-shutdown-ok", delaySeconds: 30, reason: "Test shutdown");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.Equal("test-shutdown-ok", resp.ReqId);
        Assert.True(resp.Ok);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3601)]
    public async Task Shutdown_InvalidDelay_Returns_INVALID_DELAY(int delay)
    {
        using var clientPipe = await ConnectAsync();
        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("shutdown", "test-shutdown-invalid", delaySeconds: delay, reason: "Invalid");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.Equal("test-shutdown-invalid", resp.ReqId);
        Assert.False(resp.Ok);
        Assert.Equal("INVALID_DELAY", resp.Error);
    }

    [Fact]
    public async Task CancelShutdown_Dispatches_Successfully()
    {
        using var clientPipe = await ConnectAsync();
        using var writer = new StreamWriter(clientPipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(clientPipe, Encoding.UTF8, leaveOpen: true);

        var cmd = new CommandMessage("cancel_shutdown", "test-cancel-ok");
        await writer.WriteLineAsync(IpcSerializer.Serialize(cmd).TrimEnd('\n'));

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respLine = await reader.ReadLineAsync(readCts.Token);

        Assert.NotNull(respLine);
        var resp = IpcSerializer.Deserialize<ResponseMessage>(respLine);
        Assert.NotNull(resp);
        Assert.Equal("test-cancel-ok", resp.ReqId);
        Assert.True(resp.Ok);
    }

    /// <summary>
    /// Acceptance test for Step 3 (ADR-0007 / spec §3.2):
    /// AdjustTokenPrivileges succeeds AND the token re-read confirms SE_PRIVILEGE_ENABLED is set.
    ///
    /// CI scope: runs against the developer account token (SeShutdownPrivilege is assigned
    /// and can be enabled). This confirms the AdjustTokenPrivileges + GetTokenInformation
    /// round-trip works correctly in the test runner process.
    ///
    /// OPEN: the definitive proof that the same call succeeds inside NT AUTHORITY\LocalService
    /// requires the live shutdown-with-cancel test (Step C), which calls the running service
    /// binary via IPC and observes that InitiateSystemShutdownExW returns non-zero. That test
    /// cannot be automated in CI without triggering a real shutdown; it is documented as a
    /// manual acceptance step in M1_REPORT.md, Acceptance Criteria B.
    /// </summary>
    [Fact]
    public void EnableShutdownPrivilege_AdjustsToken_AndVerifiesPrivilegeEnabled()
    {
        bool result = WindowsPowerExecutor.EnableShutdownPrivilegeAndVerify(
            out int win32Error,
            out bool privilegeEnabled);

        // On any account where SeShutdownPrivilege is assigned (developer or LocalService),
        // both must hold: the method returns true AND the re-read token bit is set.
        Assert.True(result,
            $"EnableShutdownPrivilegeAndVerify returned false. Win32 error: {win32Error}. " +
            "If running as a restricted account that lacks SeShutdownPrivilege, " +
            "this is expected; run the service binary (LocalService account) for the definitive check.");

        Assert.True(privilegeEnabled,
            "AdjustTokenPrivileges returned success but GetTokenInformation re-read shows " +
            "SE_PRIVILEGE_ENABLED is NOT set on the SeShutdownPrivilege LUID. " +
            "This indicates an OS-level rejection without an error code — " +
            "the privilege enable cannot be trusted.");
    }

    [Fact]
    public void Shutdown_RealProductionPath_WithFakeNativeApi_InvokesVerifiedPrivilegeCheck()
    {
        var fakeApi = new FakeNativePowerApi { ReturnValue = true };
        // Production path: real WindowsPrivilegeManager, fake native Win32 call
        var executor = new WindowsPowerExecutor(nativeApi: fakeApi, privilegeManager: new WindowsPrivilegeManager());

        bool ok = executor.Shutdown(45, "CI production path test", out int win32Error);

        Assert.True(ok, $"Expected Shutdown to succeed, got Win32 error {win32Error}");
        Assert.Equal(0, win32Error);
        Assert.True(fakeApi.InitiateCalled);
        Assert.Equal(45u, fakeApi.LastTimeout);
        Assert.Equal("CI production path test", fakeApi.LastMessage);
        Assert.False(fakeApi.LastReboot);
    }

    [Fact]
    public void Shutdown_WhenPrivilegeEnableFails_ReportsErrorCode_AndDoesNotCallNativeApi()
    {
        var fakePrivileges = new FakePrivilegeManager { Succeed = false, Win32Error = 1314, PrivilegeEnabled = false };
        var fakeApi = new FakeNativePowerApi { ReturnValue = true };
        var executor = new WindowsPowerExecutor(nativeApi: fakeApi, privilegeManager: fakePrivileges);

        bool ok = executor.Shutdown(30, "Privilege failure test", out int win32Error);

        Assert.False(ok);
        Assert.Equal(1314, win32Error);
        Assert.False(fakeApi.InitiateCalled, "Native API must NOT be called if privilege enable failed");
    }

    [Fact]
    public void Shutdown_WhenNativeApiFails_CapturesWin32ErrorImmediately()
    {
        var fakePrivileges = new FakePrivilegeManager { Succeed = true, Win32Error = 0, PrivilegeEnabled = true };
        var fakeApi = new FakeNativePowerApi { ReturnValue = false, SimulatedError = 1115 }; // ERROR_SHUTDOWN_IN_PROGRESS
        var executor = new WindowsPowerExecutor(nativeApi: fakeApi, privilegeManager: fakePrivileges);

        bool ok = executor.Shutdown(30, "Native API failure test", out int win32Error);

        Assert.False(ok);
        Assert.Equal(1115, win32Error);
        Assert.True(fakeApi.InitiateCalled);
    }

    [Fact]
    public void ServiceFileLog_Appends_Error_To_File()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ControlHubLogTest_{Guid.NewGuid():N}");
        string origDir = ServiceFileLog.LogDirectory;
        try
        {
            ServiceFileLog.LogDirectory = tempDir;
            ServiceFileLog.LogError("Test diagnostic error message with Win32 error 1314");

            string logFile = ServiceFileLog.LogFilePath;
            Assert.True(File.Exists(logFile), $"Log file should exist at {logFile}");
            string content = File.ReadAllText(logFile);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("Test diagnostic error message with Win32 error 1314", content);
        }
        finally
        {
            ServiceFileLog.LogDirectory = origDir;
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

internal sealed class DuplexTestStream : MemoryStream
{
    // MemoryStream acts as the stream receiving serialized writes
}

/// <summary>
/// Test double for IPowerExecutor — never calls powrprof.dll or advapi32.dll.
/// </summary>
internal sealed class NoOpPowerExecutor : IPowerExecutor
{
    public bool Sleep() => true;
    public bool Hibernate() => true;
    public bool Shutdown(int delaySeconds, string reason) => delaySeconds >= 0 && delaySeconds <= 3600;
    public bool Shutdown(int delaySeconds, string reason, out int win32Error)
    {
        win32Error = 0;
        return Shutdown(delaySeconds, reason);
    }
    public bool Restart(int delaySeconds, string reason) => delaySeconds >= 0 && delaySeconds <= 3600;
    public bool Restart(int delaySeconds, string reason, out int win32Error)
    {
        win32Error = 0;
        return Restart(delaySeconds, reason);
    }
    public bool CancelShutdown() => true;
    public bool CancelShutdown(out int win32Error)
    {
        win32Error = 0;
        return CancelShutdown();
    }
}

internal sealed class FakeNativePowerApi : INativePowerApi
{
    public bool ReturnValue { get; set; } = true;
    public int SimulatedError { get; set; } = 0;
    public bool InitiateCalled { get; private set; }
    public uint LastTimeout { get; private set; }
    public string? LastMessage { get; private set; }
    public bool LastReboot { get; private set; }

    public bool InitiateSystemShutdown(
        string? machineName,
        string? message,
        uint timeout,
        bool forceAppsClosed,
        bool rebootAfterShutdown,
        uint reason,
        out int win32Error)
    {
        InitiateCalled = true;
        LastTimeout = timeout;
        LastMessage = message;
        LastReboot = rebootAfterShutdown;
        win32Error = SimulatedError;
        return ReturnValue;
    }

    public bool AbortSystemShutdown(string? machineName, out int win32Error)
    {
        win32Error = SimulatedError;
        return ReturnValue;
    }

    public bool SetSuspend(bool hibernate, bool force, bool wakeupEventsDisabled, out int win32Error)
    {
        win32Error = SimulatedError;
        return ReturnValue;
    }
}

internal sealed class FakePrivilegeManager : IPrivilegeManager
{
    public bool Succeed { get; set; } = true;
    public int Win32Error { get; set; } = 0;
    public bool PrivilegeEnabled { get; set; } = true;

    public bool EnableShutdownPrivilegeAndVerify(out int win32Error, out bool privilegeEnabled)
    {
        win32Error = Win32Error;
        privilegeEnabled = PrivilegeEnabled;
        return Succeed;
    }
}
