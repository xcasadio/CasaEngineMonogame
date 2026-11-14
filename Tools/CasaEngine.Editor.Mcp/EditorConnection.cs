using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Mcp;

/// <summary>
/// The MCP server's link to a running editor (ADR-0092): found through the discovery files, connected on demand,
/// reconnected after the editor restarts. Thread-safe; one editor call at a time.
/// </summary>
public sealed class EditorConnection : IAsyncDisposable
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(2);

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
            for (int attempt = 0; attempt < 2; attempt++)
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
                    // The editor closed or restarted: drop the client and try once with a fresh connection.
                    await DropClientAsync();
                    if (attempt == 1)
                    {
                        return (null, $"The connection to the editor was lost during '{command}': {ex.Message}");
                    }
                }
            }

            return (null, EditorToolCatalog.NoEditorMessage);
        }
        finally
        {
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
