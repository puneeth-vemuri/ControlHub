namespace ControlHub.Service.Core;

/// <summary>
/// Abstraction for process token privilege adjustments.
/// </summary>
public interface IPrivilegeManager
{
    /// <summary>
    /// Enables SeShutdownPrivilege on the process token and verifies via token readback that
    /// the privilege is actively enabled.
    /// </summary>
    bool EnableShutdownPrivilegeAndVerify(out int win32Error, out bool privilegeEnabled);
}
