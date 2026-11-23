namespace ControlHub.IpcContracts;

public static class PipeNames
{
    // Service hosts this pipe; the FastAPI agent connects as client to send commands
    public const string ServicePipe = "ControlHubService";
    // Service hosts this pipe; the companion connects as client
    public const string CompanionPipe = "ControlHubCompanion";
}
