namespace ControlHub.Service.Core;

/// <summary>
/// Abstracts machine-level power operations (sleep/hibernate via SetSuspendState, shutdown/restart via InitiateSystemShutdownExW).
/// The real implementation calls Windows APIs; a test double can be injected in tests.
/// </summary>
public interface IPowerExecutor
{
    /// <summary>Suspend the machine to sleep. bForce is always false.</summary>
    bool Sleep();

    /// <summary>Suspend the machine to hibernate. bForce is always false.</summary>
    bool Hibernate();

    /// <summary>Initiate a timed shutdown with delay in seconds and a reason string.</summary>
    bool Shutdown(int delaySeconds, string reason);

    /// <summary>Initiate a timed shutdown and return the Win32 error code on failure.</summary>
    bool Shutdown(int delaySeconds, string reason, out int win32Error);

    /// <summary>Initiate a timed restart with delay in seconds and a reason string.</summary>
    bool Restart(int delaySeconds, string reason);

    /// <summary>Initiate a timed restart and return the Win32 error code on failure.</summary>
    bool Restart(int delaySeconds, string reason, out int win32Error);

    /// <summary>Cancel a pending timed shutdown or restart.</summary>
    bool CancelShutdown();

    /// <summary>Cancel a pending timed shutdown or restart and return the Win32 error code on failure.</summary>
    bool CancelShutdown(out int win32Error);
}
