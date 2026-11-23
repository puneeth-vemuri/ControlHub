namespace ControlHub.Service.Core;

/// <summary>
/// Abstraction for native Win32 power calls, allowing test doubles in CI.
/// </summary>
public interface INativePowerApi
{
    /// <summary>
    /// Invokes the underlying OS shutdown or reboot function, capturing the Win32 error immediately on failure.
    /// </summary>
    bool InitiateSystemShutdown(
        string? machineName,
        string? message,
        uint timeout,
        bool forceAppsClosed,
        bool rebootAfterShutdown,
        uint reason,
        out int win32Error);

    /// <summary>
    /// Invokes the underlying OS abort shutdown function, capturing the Win32 error immediately on failure.
    /// </summary>
    bool AbortSystemShutdown(string? machineName, out int win32Error);

    /// <summary>
    /// Invokes the underlying OS suspend function, capturing the Win32 error immediately on failure.
    /// </summary>
    bool SetSuspend(bool hibernate, bool force, bool wakeupEventsDisabled, out int win32Error);
}
