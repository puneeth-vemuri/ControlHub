using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlHub.IpcContracts;

// Note: We use standard classes (not positional record parameters) for JSON attributes
// to avoid compiler warning CS0657 ("property" is not a valid attribute location for
// positional record parameters - must use "param:" target). C# positional record
// parameters do not support [property:] JSON attributes cleanly before C# 12.

public class IpcMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("req_id")]
    public string? ReqId { get; init; }

    public IpcMessage() { }
    public IpcMessage(string type, string? reqId = null) { Type = type; ReqId = reqId; }
}

public sealed class CommandMessage : IpcMessage
{
    [JsonPropertyName("cmd")]
    public string Cmd { get; init; } = "";

    [JsonPropertyName("delay_seconds")]
    public int? DelaySeconds { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    public CommandMessage() : base("cmd") { }

    public CommandMessage(string cmd, string reqId, int? delaySeconds = null, string? reason = null)
        : base("cmd", reqId)
    {
        Cmd = cmd;
        DelaySeconds = delaySeconds;
        Reason = reason;
    }
}

public sealed class ResponseMessage : IpcMessage
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    public ResponseMessage() : base("resp") { }
    public ResponseMessage(string reqId, bool ok, string? error = null, object? data = null)
        : base("resp", reqId)
    {
        Ok = ok;
        Error = error;
        Data = data;
    }
}

public sealed class StatusMessage : IpcMessage
{
    [JsonPropertyName("companion_online")]
    public bool CompanionOnline { get; init; }

    [JsonPropertyName("session_user")]
    public string SessionUser { get; init; } = "";

    [JsonPropertyName("ts")]
    public string Ts { get; init; } = "";

    public StatusMessage() : base("status") { }
    public StatusMessage(bool companionOnline, string sessionUser, string ts)
        : base("status")
    {
        CompanionOnline = companionOnline;
        SessionUser = sessionUser;
        Ts = ts;
    }
}

public static class IpcSerializer
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T msg) where T : IpcMessage
        => JsonSerializer.Serialize(msg, Opts) + "\n";

    public static T? Deserialize<T>(string json) where T : IpcMessage
        => JsonSerializer.Deserialize<T>(json, Opts);

    public static IpcMessage? DeserializeBase(string json)
        => JsonSerializer.Deserialize<IpcMessage>(json, Opts);
}
