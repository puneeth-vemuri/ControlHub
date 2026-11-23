namespace ControlHub.Service.Core;

/// <summary>
/// Thread-safe file logger that writes timestamped entries to C:\ProgramData\ControlHub\logs\service.log.
/// Ensures diagnostic failure evidence is preserved even if Windows EventLog registration fails.
/// </summary>
public static class ServiceFileLog
{
    private static readonly object FileLock = new();

    public static string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ControlHub",
        "logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "service.log");

    public static void Log(string level, string message)
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            var line = $"[{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] [{level}] {message}{Environment.NewLine}";
            lock (FileLock)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Logging must never crash the service
        }
    }

    public static void LogInfo(string message) => Log("INFO", message);
    public static void LogWarning(string message) => Log("WARN", message);
    public static void LogError(string message) => Log("ERROR", message);
}
