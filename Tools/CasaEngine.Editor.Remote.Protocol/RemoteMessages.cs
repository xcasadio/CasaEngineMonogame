using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasaEngine.Editor.Remote.Protocol;

/// <summary>Constants of the editor remote control protocol (ADR-0092).</summary>
public static class RemoteProtocol
{
    /// <summary>Incremented on any incompatible change of the messages.</summary>
    public const int Version = 1;

    /// <summary>Longest accepted message line, in bytes, without its end of line.</summary>
    public const int MaxMessageBytes = 1024 * 1024;

    public const string JsonRpcVersion = "2.0";

    /// <summary>Handshake: protocol version, editor version and the token of the discovery file.</summary>
    public const string HelloMethod = "editor.hello";

    /// <summary>The commands of the editor, with their argument schemas.</summary>
    public const string ListCommandsMethod = "commands.list";

    /// <summary>Options shared by every reader and writer: snake_case names, no indentation.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = false,
    };
}

/// <summary>JSON-RPC error codes: the standard ones, then those of the editor.</summary>
public static class RemoteErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    /// <summary>The editor refuses the command, see <see cref="RemoteErrorReasons"/>.</summary>
    public const int CommandRefused = -32000;
}

/// <summary>Machine-readable reason of a <see cref="RemoteErrorCodes.CommandRefused"/> error.</summary>
public static class RemoteErrorReasons
{
    public const string PlayModeActive = "play_mode_active";
    public const string UnsavedChanges = "unsaved_changes";
    public const string NotFound = "not_found";
    public const string InvalidArgument = "invalid_argument";
    public const string Timeout = "timeout";
    public const string Failed = "failed";
    public const string MessageTooLong = "message_too_long";
}

public sealed class RemoteRequest
{
    public string Jsonrpc { get; init; } = RemoteProtocol.JsonRpcVersion;

    public long Id { get; init; }

    public string Method { get; init; } = string.Empty;

    public JsonElement? Params { get; init; }
}

public sealed class RemoteError
{
    public int Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public RemoteErrorData? Data { get; init; }

    public static RemoteError Refused(string reason, string message)
        => new() { Code = RemoteErrorCodes.CommandRefused, Message = message, Data = new RemoteErrorData { Reason = reason } };

    public override string ToString() => Data?.Reason is { } reason ? $"{Code} ({reason}): {Message}" : $"{Code}: {Message}";
}

public sealed class RemoteErrorData
{
    public string? Reason { get; init; }
}

public sealed class RemoteResponse
{
    public string Jsonrpc { get; init; } = RemoteProtocol.JsonRpcVersion;

    /// <summary>Id of the request; null when the request could not be read.</summary>
    public long? Id { get; init; }

    public JsonElement? Result { get; init; }

    public RemoteError? Error { get; init; }

    public static RemoteResponse Success(long id, JsonElement result) => new() { Id = id, Result = result };

    public static RemoteResponse Failure(long? id, RemoteError error) => new() { Id = id, Error = error };
}

/// <summary>Result of <see cref="RemoteProtocol.HelloMethod"/>.</summary>
public sealed class RemoteHelloResult
{
    public int ProtocolVersion { get; init; }

    public string EditorVersion { get; init; } = string.Empty;

    public int ProcessId { get; init; }

    public string Token { get; init; } = string.Empty;
}

/// <summary>One command of <see cref="RemoteProtocol.ListCommandsMethod"/>.</summary>
public sealed class RemoteCommandDescriptor
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>JSON schema of the arguments; always an object schema.</summary>
    public JsonElement InputSchema { get; init; }

    /// <summary>The command reads the editor state and changes nothing.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>The command can delete or overwrite project data, or run project code.</summary>
    public bool Destructive { get; init; }
}

public sealed class RemoteCommandList
{
    public IReadOnlyList<RemoteCommandDescriptor> Commands { get; init; } = Array.Empty<RemoteCommandDescriptor>();
}
