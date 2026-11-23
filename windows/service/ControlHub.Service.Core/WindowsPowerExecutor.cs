using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ControlHub.Service.Core;

/// <summary>
/// Production implementation of IPowerExecutor.
/// Routes privileged power operations through IPrivilegeManager and INativePowerApi,
/// ensuring Win32 errors are captured immediately on failure and logged to both
/// Microsoft.Extensions.Logging and ServiceFileLog.
/// </summary>
public sealed class WindowsPowerExecutor : IPowerExecutor
{
    private readonly INativePowerApi _nativeApi;
    private readonly IPrivilegeManager _privilegeManager;
    private readonly ILogger<WindowsPowerExecutor>? _logger;

    private const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;
    private const uint SHTDN_REASON_MAJOR_OTHER = 0x00000000;
    private const uint SHTDN_REASON_MINOR_OTHER = 0x00000000;

    public WindowsPowerExecutor(
        INativePowerApi? nativeApi = null,
        IPrivilegeManager? privilegeManager = null,
        ILogger<WindowsPowerExecutor>? logger = null)
    {
        _nativeApi = nativeApi ?? new DefaultNativePowerApi();
        _privilegeManager = privilegeManager ?? new WindowsPrivilegeManager();
        _logger = logger;
    }

    /// <summary>
    /// Static helper exposing privilege verification directly, forwarding to WindowsPrivilegeManager.
    /// Used by standalone integration tests.
    /// </summary>
    public static bool EnableShutdownPrivilegeAndVerify(out int win32Error, out bool privilegeEnabled)
    {
        return WindowsPrivilegeManager.Default.EnableShutdownPrivilegeAndVerify(out win32Error, out privilegeEnabled);
    }

    /// <inheritdoc/>
    public bool Sleep()
    {
        // ADR-0007 requirement: bForce MUST be false so applications can veto.
        bool ok = _nativeApi.SetSuspend(hibernate: false, force: false, wakeupEventsDisabled: false, out int err);
        if (!ok)
        {
            ServiceFileLog.LogError($"SetSuspendState(sleep) failed with Win32 error {err}");
            _logger?.LogError("SetSuspendState(sleep) failed with Win32 error {Win32Error}", err);
        }
        return ok;
    }

    /// <inheritdoc/>
    public bool Hibernate()
    {
        // ADR-0007 requirement: bForce MUST be false so applications can veto.
        bool ok = _nativeApi.SetSuspend(hibernate: true, force: false, wakeupEventsDisabled: false, out int err);
        if (!ok)
        {
            ServiceFileLog.LogError($"SetSuspendState(hibernate) failed with Win32 error {err}");
            _logger?.LogError("SetSuspendState(hibernate) failed with Win32 error {Win32Error}", err);
        }
        return ok;
    }

    /// <inheritdoc/>
    public bool Shutdown(int delaySeconds, string reason)
    {
        return Shutdown(delaySeconds, reason, out _);
    }

    /// <inheritdoc/>
    public bool Shutdown(int delaySeconds, string reason, out int win32Error)
    {
        win32Error = 0;
        if (delaySeconds < 0 || delaySeconds > 3600)
        {
            win32Error = 87; // ERROR_INVALID_PARAMETER
            ServiceFileLog.LogWarning($"Shutdown rejected: delaySeconds {delaySeconds} out of range [0, 3600]");
            _logger?.LogWarning("Shutdown rejected: delaySeconds {Delay} out of range [0, 3600]", delaySeconds);
            return false;
        }

        if (!_privilegeManager.EnableShutdownPrivilegeAndVerify(out int privErr, out bool privilegeEnabled) || !privilegeEnabled)
        {
            win32Error = privErr != 0 ? privErr : 1300; // 1300 = ERROR_NOT_ALL_ASSIGNED
            ServiceFileLog.LogError($"Shutdown failed: EnableShutdownPrivilegeAndVerify failed with error {win32Error}, privilegeEnabled={privilegeEnabled}");
            _logger?.LogError("Shutdown failed: EnableShutdownPrivilegeAndVerify failed with error {Win32Error}, privilegeEnabled={PrivilegeEnabled}", win32Error, privilegeEnabled);
            return false;
        }

        uint reasonCode = SHTDN_REASON_FLAG_PLANNED | SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_MINOR_OTHER;
        bool ok = _nativeApi.InitiateSystemShutdown(
            null,
            reason,
            (uint)delaySeconds,
            forceAppsClosed: true,
            rebootAfterShutdown: false,
            reasonCode,
            out int apiErr);

        if (!ok)
        {
            win32Error = apiErr;
            ServiceFileLog.LogError($"InitiateSystemShutdownExW (shutdown) failed with Win32 error {win32Error}");
            _logger?.LogError("InitiateSystemShutdownExW (shutdown) failed with Win32 error {Win32Error}", win32Error);
            return false;
        }

        ServiceFileLog.LogInfo($"InitiateSystemShutdownExW (shutdown) succeeded: delay={delaySeconds}s, reason='{reason}'");
        _logger?.LogInformation("InitiateSystemShutdownExW (shutdown) succeeded: delay={Delay}s", delaySeconds);
        return true;
    }

    /// <inheritdoc/>
    public bool Restart(int delaySeconds, string reason)
    {
        return Restart(delaySeconds, reason, out _);
    }

    /// <inheritdoc/>
    public bool Restart(int delaySeconds, string reason, out int win32Error)
    {
        win32Error = 0;
        if (delaySeconds < 0 || delaySeconds > 3600)
        {
            win32Error = 87; // ERROR_INVALID_PARAMETER
            ServiceFileLog.LogWarning($"Restart rejected: delaySeconds {delaySeconds} out of range [0, 3600]");
            _logger?.LogWarning("Restart rejected: delaySeconds {Delay} out of range [0, 3600]", delaySeconds);
            return false;
        }

        if (!_privilegeManager.EnableShutdownPrivilegeAndVerify(out int privErr, out bool privilegeEnabled) || !privilegeEnabled)
        {
            win32Error = privErr != 0 ? privErr : 1300; // 1300 = ERROR_NOT_ALL_ASSIGNED
            ServiceFileLog.LogError($"Restart failed: EnableShutdownPrivilegeAndVerify failed with error {win32Error}, privilegeEnabled={privilegeEnabled}");
            _logger?.LogError("Restart failed: EnableShutdownPrivilegeAndVerify failed with error {Win32Error}, privilegeEnabled={PrivilegeEnabled}", win32Error, privilegeEnabled);
            return false;
        }

        uint reasonCode = SHTDN_REASON_FLAG_PLANNED | SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_MINOR_OTHER;
        bool ok = _nativeApi.InitiateSystemShutdown(
            null,
            reason,
            (uint)delaySeconds,
            forceAppsClosed: true,
            rebootAfterShutdown: true,
            reasonCode,
            out int apiErr);

        if (!ok)
        {
            win32Error = apiErr;
            ServiceFileLog.LogError($"InitiateSystemShutdownExW (restart) failed with Win32 error {win32Error}");
            _logger?.LogError("InitiateSystemShutdownExW (restart) failed with Win32 error {Win32Error}", win32Error);
            return false;
        }

        ServiceFileLog.LogInfo($"InitiateSystemShutdownExW (restart) succeeded: delay={delaySeconds}s, reason='{reason}'");
        _logger?.LogInformation("InitiateSystemShutdownExW (restart) succeeded: delay={Delay}s", delaySeconds);
        return true;
    }

    /// <inheritdoc/>
    public bool CancelShutdown()
    {
        return CancelShutdown(out _);
    }

    /// <inheritdoc/>
    public bool CancelShutdown(out int win32Error)
    {
        win32Error = 0;
        if (!_privilegeManager.EnableShutdownPrivilegeAndVerify(out int privErr, out bool privilegeEnabled) || !privilegeEnabled)
        {
            win32Error = privErr != 0 ? privErr : 1300;
            ServiceFileLog.LogError($"CancelShutdown failed: EnableShutdownPrivilegeAndVerify failed with error {win32Error}, privilegeEnabled={privilegeEnabled}");
            _logger?.LogError("CancelShutdown failed: EnableShutdownPrivilegeAndVerify failed with error {Win32Error}, privilegeEnabled={PrivilegeEnabled}", win32Error, privilegeEnabled);
            return false;
        }

        bool ok = _nativeApi.AbortSystemShutdown(null, out int apiErr);
        if (!ok)
        {
            win32Error = apiErr;
            ServiceFileLog.LogError($"AbortSystemShutdownW failed with Win32 error {win32Error}");
            _logger?.LogError("AbortSystemShutdownW failed with Win32 error {Win32Error}", win32Error);
            return false;
        }

        ServiceFileLog.LogInfo("AbortSystemShutdownW succeeded");
        _logger?.LogInformation("AbortSystemShutdownW succeeded");
        return true;
    }
}

/// <summary>
/// Production implementation of INativePowerApi calling advapi32.dll and powrprof.dll.
/// Captures Marshal.GetLastWin32Error() immediately on any false return.
/// </summary>
public sealed class DefaultNativePowerApi : INativePowerApi
{
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool InitiateSystemShutdownExW(
        string? lpMachineName,
        string? lpMessage,
        uint dwTimeout,
        bool bForceAppsClosed,
        bool bRebootAfterShutdown,
        uint dwReason);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool AbortSystemShutdownW(string? lpMachineName);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool bHibernate, bool bForce, bool bWakeupEventsDisabled);

    public bool InitiateSystemShutdown(
        string? machineName,
        string? message,
        uint timeout,
        bool forceAppsClosed,
        bool rebootAfterShutdown,
        uint reason,
        out int win32Error)
    {
        win32Error = 0;
        bool ok = InitiateSystemShutdownExW(machineName, message, timeout, forceAppsClosed, rebootAfterShutdown, reason);
        if (!ok)
        {
            win32Error = Marshal.GetLastWin32Error();
        }
        return ok;
    }

    public bool AbortSystemShutdown(string? machineName, out int win32Error)
    {
        win32Error = 0;
        bool ok = AbortSystemShutdownW(machineName);
        if (!ok)
        {
            win32Error = Marshal.GetLastWin32Error();
        }
        return ok;
    }

    public bool SetSuspend(bool hibernate, bool force, bool wakeupEventsDisabled, out int win32Error)
    {
        win32Error = 0;
        bool ok = SetSuspendState(hibernate, force, wakeupEventsDisabled);
        if (!ok)
        {
            win32Error = Marshal.GetLastWin32Error();
        }
        return ok;
    }
}

/// <summary>
/// Production implementation of IPrivilegeManager calling OpenProcessToken, AdjustTokenPrivileges,
/// and GetTokenInformation to enable and verify SeShutdownPrivilege.
/// </summary>
public sealed class WindowsPrivilegeManager : IPrivilegeManager
{
    public static readonly WindowsPrivilegeManager Default = new();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TokenPrivileges = 3;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr TokenHandle,
        uint TokenInformationClass,
        IntPtr TokenInformation,
        uint TokenInformationLength,
        out uint ReturnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    public bool EnableShutdownPrivilegeAndVerify(out int win32Error, out bool privilegeEnabled)
    {
        privilegeEnabled = false;
        win32Error = 0;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
        {
            win32Error = Marshal.GetLastWin32Error();
            return false;
        }

        try
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out LUID luid))
            {
                win32Error = Marshal.GetLastWin32Error();
                return false;
            }

            TOKEN_PRIVILEGES tp = new()
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }
            };

            bool adjusted = AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            int adjustError = Marshal.GetLastWin32Error();
            if (!adjusted || adjustError != 0)
            {
                win32Error = adjustError != 0 ? adjustError : 1;
                return false;
            }

            // Re-read token privileges to verify the bit is actually set
            GetTokenInformation(hToken, TokenPrivileges, IntPtr.Zero, 0, out uint needed);
            if (needed == 0)
            {
                win32Error = Marshal.GetLastWin32Error();
                return false;
            }

            IntPtr buf = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!GetTokenInformation(hToken, TokenPrivileges, buf, needed, out _))
                {
                    win32Error = Marshal.GetLastWin32Error();
                    return false;
                }

                uint count = (uint)Marshal.ReadInt32(buf);
                int offset = sizeof(uint);
                int entrySize = Marshal.SizeOf<LUID_AND_ATTRIBUTES>();

                for (uint i = 0; i < count; i++)
                {
                    var entry = Marshal.PtrToStructure<LUID_AND_ATTRIBUTES>(buf + offset + (int)(i * entrySize));
                    if (entry.Luid.LowPart == luid.LowPart && entry.Luid.HighPart == luid.HighPart)
                    {
                        privilegeEnabled = (entry.Attributes & SE_PRIVILEGE_ENABLED) != 0;
                        return true;
                    }
                }

                win32Error = 1300; // ERROR_NOT_ALL_ASSIGNED
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            CloseHandle(hToken);
        }
    }
}
