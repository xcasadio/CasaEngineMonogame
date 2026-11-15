using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Mcp;

/// <summary>How a call to the editor ended.</summary>
public enum EditorCallStatus
{
    Answered,

    /// <summary>The editor refused the command or the connection broke during the call.</summary>
    Failed,

    /// <summary>No editor serves remote control.</summary>
    NoEditor,

    /// <summary>An editor serves remote control but did not accept the connection in time (other clients).</summary>
    Busy,
}

/// <summary>The result of <see cref="EditorConnection.CallAsync"/>.</summary>
public readonly record struct EditorCallResult(EditorCallStatus Status, JsonElement? Result, string? Error);

/// <summary>
/// The MCP server's link to a running editor (ADR-0092, ADR-0093): found through the discovery files, connected for each
/// call and released right after it. An editor found but not reachable in time is reported as busy, not as absent.
/// Thread-safe; one editor call at a time.
/// </summary>
public sealed class EditorConnection : IAsyncDisposable
{
    public const string BusyMessage = "The CasaEngine editor is busy with other remote clients: try again in a moment.";

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Longer than the editor's own answer timeout (5 min), so the editor's timeout answer arrives first.</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(6);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int? _processId;
    private readonly string _discoveryDirectory;
    private readonly TimeSpan _connectTimeout;
    private RemoteEditorClient? _client;
    private bool _disposed;

    public EditorConnection(int? processId, string? discoveryDirectory = null, TimeSpan? connectTimeout = null)
    {
        _processId = processId;
        _discoveryDirectory = discoveryDirectory ?? RemoteDiscovery.DefaultDirectory;
        _connectTimeout = connectTimeout ?? ConnectTimeout;
    }

    /// <summary>The editor's command list; on any other status the reason is in the result.</summary>
    public async Task<(IReadOnlyList<RemoteCommandDescriptor>? Commands, EditorCallResult Call)> ListCommandsAsync(CancellationToken cancellationToken)
    {
        EditorCallResult call = await CallAsync(RemoteProtocol.ListCommandsMethod, null, cancellationToken);
        if (call.Status != EditorCallStatus.Answered)
        {
            return (null, call);
        }

        return (RemoteMessageCodec.FromElement<RemoteCommandList>(call.Result!.Value)?.Commands ?? Array.Empty<RemoteCommandDescriptor>(), call);
    }

    /// <summary>Calls a command; a refused command, a lost, absent or busy editor comes back as a status with a message.</summary>
    public async Task<EditorCallResult> CallAsync(string command, JsonElement? parameters, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            (RemoteEditorClient? client, bool busy) = await ConnectAsync(cancellationToken);
            if (client == null)
            {
                return busy
                    ? new EditorCallResult(EditorCallStatus.Busy, null, BusyMessage)
                    : new EditorCallResult(EditorCallStatus.NoEditor, null, EditorToolCatalog.NoEditorMessage);
            }

            try
            {
                return new EditorCallResult(EditorCallStatus.Answered, await client.CallAsync(command, parameters, CallTimeout, cancellationToken), null);
            }
            catch (RemoteEditorException ex)
            {
                return new EditorCallResult(EditorCallStatus.Failed, null, $"The editor refused '{command}': {ex.Error}");
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return new EditorCallResult(EditorCallStatus.Failed, null, $"The connection to the editor was lost during '{command}': {ex.Message}");
            }
        }
        finally
        {
            // Release the pipe after every call, so the CLI, another agent or the next poll can connect (verifier finding F1).
            await DropClientAsync();
            _gate.Release();
        }
    }

    /// <summary>Waits for a call in progress, then closes; a later call throws <see cref="ObjectDisposedException"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _disposed = true;
            await DropClientAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(RemoteEditorClient? Client, bool Busy)> ConnectAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RemoteDiscoveryRecord> editors = RemoteDiscovery.ReadAll(_discoveryDirectory);
        RemoteDiscoveryRecord? record = _processId.HasValue
            ? editors.FirstOrDefault(e => e.ProcessId == _processId.Value)
            : editors.Count == 1 ? editors[0] : editors.OrderByDescending(e => e.ProcessStartUtcTicks).FirstOrDefault();
        if (record == null)
        {
            return (null, false);
        }

        try
        {
            _client = await RemoteEditorClient.ConnectAsync(record, _connectTimeout, cancellationToken);
            return (_client, false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // A live editor (the discovery record checks the process) that does not accept us in time is busy.
            return (null, true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or RemoteEditorException)
        {
            return (null, false);
        }
    }

    private async Task DropClientAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
