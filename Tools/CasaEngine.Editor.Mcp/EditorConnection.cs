using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Mcp;

/// <summary>
/// The MCP server's link to a running editor (ADR-0092): found through the discovery files, connected for each call
/// and released right after it, since the editor serves one client at a time. Thread-safe; one editor call at a time.
/// </summary>
public sealed class EditorConnection : IAsyncDisposable
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    /// <summary>Longer than the editor's own answer timeout (5 min), so the editor's timeout answer arrives first.</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(6);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int? _processId;
    private readonly string _discoveryDirectory;
    private RemoteEditorClient? _client;

    public EditorConnection(int? processId, string? discoveryDirectory = null)
    {
        _processId = processId;
        _discoveryDirectory = discoveryDirectory ?? RemoteDiscovery.DefaultDirectory;
    }

    /// <summary>The editor's command list, or null with a reason when no editor can be reached.</summary>
    public async Task<(IReadOnlyList<RemoteCommandDescriptor>? Commands, string? Error)> ListCommandsAsync(CancellationToken cancellationToken)
    {
        (JsonElement? result, string? error) = await CallAsync(RemoteProtocol.ListCommandsMethod, null, cancellationToken);
        if (result == null)
        {
            return (null, error);
        }

        return (RemoteMessageCodec.FromElement<RemoteCommandList>(result.Value)?.Commands ?? Array.Empty<RemoteCommandDescriptor>(), null);
    }

    /// <summary>Calls a command; a refused command or a lost editor comes back as an error message.</summary>
    public async Task<(JsonElement? Result, string? Error)> CallAsync(string command, JsonElement? parameters, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RemoteEditorClient? client = await GetClientAsync(cancellationToken);
            if (client == null)
            {
                return (null, EditorToolCatalog.NoEditorMessage);
            }

            try
            {
                return (await client.CallAsync(command, parameters, CallTimeout, cancellationToken), null);
            }
            catch (RemoteEditorException ex)
            {
                return (null, $"The editor refused '{command}': {ex.Error}");
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return (null, $"The connection to the editor was lost during '{command}': {ex.Message}");
            }
        }
        finally
        {
            // The editor serves one client at a time: release the pipe after every call, so the CLI, another agent
            // or the next poll can connect (verifier finding F1).
            await DropClientAsync();
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DropClientAsync();
        _gate.Dispose();
    }

    private async Task<RemoteEditorClient?> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client != null)
        {
            return _client;
        }

        IReadOnlyList<RemoteDiscoveryRecord> editors = RemoteDiscovery.ReadAll(_discoveryDirectory);
        RemoteDiscoveryRecord? record = _processId.HasValue
            ? editors.FirstOrDefault(e => e.ProcessId == _processId.Value)
            : editors.Count == 1 ? editors[0] : editors.OrderByDescending(e => e.ProcessStartUtcTicks).FirstOrDefault();
        if (record == null)
        {
            return null;
        }

        try
        {
            _client = await RemoteEditorClient.ConnectAsync(record, ConnectTimeout, cancellationToken);
            return _client;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or RemoteEditorException
            or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
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
