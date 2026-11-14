using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// One request of a remote client, executed on the game thread by <see cref="EditorRemoteDispatcher"/> (ADR-0092).
/// The handler answers with <see cref="Succeed{T}"/> or <see cref="Fail"/>, or defers the answer with
/// <see cref="Defer"/>; the pipe thread waits on <see cref="Completion"/>.
/// </summary>
internal sealed class EditorRemoteCall
{
    private readonly TaskCompletionSource<RemoteResponse> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EditorRemoteCall(RemoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public RemoteRequest Request { get; }

    public long Id => Request.Id;

    public string Method => Request.Method;

    public Task<RemoteResponse> Completion => _completion.Task;

    public bool IsCompleted => _completion.Task.IsCompleted;

    internal Func<EditorRemoteCall, bool> DeferredPoll { get; private set; }

    internal double DeferredTimeoutSeconds { get; private set; }

    internal double DeferredDeadline { get; set; }

    public bool IsDeferred => DeferredPoll != null;

    public void Succeed<T>(T result)
    {
        _completion.TrySetResult(RemoteResponse.Success(Id, RemoteMessageCodec.ToElement(result)));
    }

    public void Fail(RemoteError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _completion.TrySetResult(RemoteResponse.Failure(Id, error));
    }

    /// <summary>
    /// Answers later: <paramref name="poll"/> runs once per editor frame on the game thread and returns true once it
    /// has completed the call. After <paramref name="timeoutSeconds"/> the call fails with
    /// <see cref="RemoteErrorReasons.Timeout"/>.
    /// </summary>
    public void Defer(Func<EditorRemoteCall, bool> poll, double timeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(poll);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutSeconds);
        DeferredPoll = poll;
        DeferredTimeoutSeconds = timeoutSeconds;
    }

    public bool HasArgument(string name)
        => Request.Params is { } parameters
            && parameters.TryGetProperty(name, out JsonElement value)
            && value.ValueKind != JsonValueKind.Null;

    public JsonElement GetArgument(string name)
    {
        if (Request.Params is { } parameters && parameters.TryGetProperty(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null)
        {
            return value;
        }

        throw new EditorRemoteArgumentException($"Argument '{name}' is missing.");
    }

    public string GetString(string name, int maxLength = 1024)
    {
        JsonElement value = GetArgument(name);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new EditorRemoteArgumentException($"Argument '{name}' must be a string.");
        }

        string text = value.GetString();
        if (text.Length > maxLength)
        {
            throw new EditorRemoteArgumentException(string.Create(CultureInfo.InvariantCulture, $"Argument '{name}' is longer than {maxLength} characters."));
        }

        return text;
    }

    public string GetOptionalString(string name, int maxLength = 1024) => HasArgument(name) ? GetString(name, maxLength) : null;

    public Guid GetGuid(string name)
    {
        string text = GetString(name, 64);
        if (!Guid.TryParse(text, out Guid id))
        {
            throw new EditorRemoteArgumentException($"Argument '{name}' is not a GUID: '{text}'.");
        }

        return id;
    }

    public Guid? GetOptionalGuid(string name) => HasArgument(name) ? GetGuid(name) : null;

    public bool GetOptionalBool(string name, bool fallback)
    {
        if (!HasArgument(name))
        {
            return fallback;
        }

        JsonElement value = GetArgument(name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new EditorRemoteArgumentException($"Argument '{name}' must be true or false."),
        };
    }

    public int GetOptionalInt(string name, int fallback, int min, int max)
    {
        if (!HasArgument(name))
        {
            return fallback;
        }

        JsonElement value = GetArgument(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number) || number < min || number > max)
        {
            throw new EditorRemoteArgumentException(string.Create(CultureInfo.InvariantCulture, $"Argument '{name}' must be an integer from {min} to {max}."));
        }

        return number;
    }
}

/// <summary>A missing or malformed argument; answered as <see cref="RemoteErrorCodes.InvalidParams"/>.</summary>
internal sealed class EditorRemoteArgumentException : Exception
{
    public EditorRemoteArgumentException(string message)
        : base(message)
    {
    }
}

/// <summary>A command the editor refuses in its current state; answered with a <see cref="RemoteErrorReasons"/> reason.</summary>
internal sealed class EditorRemoteRefusedException : Exception
{
    public EditorRemoteRefusedException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
