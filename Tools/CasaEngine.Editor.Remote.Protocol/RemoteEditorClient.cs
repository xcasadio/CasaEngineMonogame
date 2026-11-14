using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;

namespace CasaEngine.Editor.Remote.Protocol;

/// <summary>
/// Client side of the editor pipe, shared by the CLI and the MCP server (ADR-0092). It connects to the local machine
/// only, requires the server to belong to the current user (<see cref="PipeOptions.CurrentUserOnly"/>), lets the
/// server identify but not impersonate it, and checks the token of the discovery record. One request at a time.
/// </summary>
public sealed class RemoteEditorClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly RemoteLineReader _reader;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _nextId;

    private RemoteEditorClient(NamedPipeClientStream pipe, RemoteDiscoveryRecord record)
    {
        _pipe = pipe;
        _reader = new RemoteLineReader(pipe);
        Record = record;
    }

    public RemoteDiscoveryRecord Record { get; }

    public RemoteHelloResult? Hello { get; private set; }

    /// <summary>Connects to <paramref name="record"/>'s pipe and runs the handshake.</summary>
    public static async Task<RemoteEditorClient> ConnectAsync(RemoteDiscoveryRecord record, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!RemoteDiscovery.IsValidPipeName(record.PipeName))
        {
            throw new RemoteEditorException(RemoteError.Refused(RemoteErrorReasons.InvalidArgument, $"'{record.PipeName}' is not an editor pipe name."));
        }

        var pipe = new NamedPipeClientStream(
            ".",
            record.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            TokenImpersonationLevel.Identification);

        var client = new RemoteEditorClient(pipe, record);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            await pipe.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);

            JsonElement helloElement = await client.CallAsync(RemoteProtocol.HelloMethod, null, timeout, cancellationToken).ConfigureAwait(false);
            RemoteHelloResult? hello = RemoteMessageCodec.FromElement<RemoteHelloResult>(helloElement);
            if (hello == null || hello.Token != record.Token || hello.ProtocolVersion != RemoteProtocol.Version)
            {
                throw new RemoteEditorException(RemoteError.Refused(RemoteErrorReasons.InvalidArgument, "The pipe did not answer with the editor's token or protocol version."));
            }

            client.Hello = hello;
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Calls a method and returns its result; an error response throws <see cref="RemoteEditorException"/>.</summary>
    public async Task<JsonElement> CallAsync(string method, JsonElement? parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            long id = Interlocked.Increment(ref _nextId);
            await RemoteMessageCodec.WriteAsync(_pipe, new RemoteRequest { Id = id, Method = method, Params = parameters }, timeoutSource.Token).ConfigureAwait(false);

            RemoteLine line = await _reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
            if (line.Status != RemoteLineStatus.Line)
            {
                throw new IOException(line.Status == RemoteLineStatus.EndOfStream
                    ? "The editor closed the connection."
                    : "The editor sent a message longer than the protocol limit.");
            }

            if (!RemoteMessageCodec.TryDecodeResponse(line.Text, out RemoteResponse response) || response.Id != id)
            {
                throw new IOException("The editor sent an invalid response.");
            }

            if (response.Error != null)
            {
                throw new RemoteEditorException(response.Error);
            }

            return response.Result!.Value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _pipe.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}

/// <summary>An error response of the editor.</summary>
public sealed class RemoteEditorException : Exception
{
    public RemoteEditorException(RemoteError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public RemoteError Error { get; }
}
