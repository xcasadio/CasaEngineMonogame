using System.Globalization;
using CasaEngine.Editor.Mcp;
using CasaEngine.Editor.Remote.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// casa-editor-mcp: MCP server on stdio that exposes the commands of a running CasaEngine editor (ADR-0092).
// Usage: casa-editor-mcp [--pid <editor process id>]. Nothing but the protocol may be written to stdout.

int? processId = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--pid" && i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
    {
        processId = pid;
        i++;
    }
    else
    {
        Console.Error.WriteLine($"casa-editor-mcp: unknown argument '{args[i]}'. Usage: casa-editor-mcp [--pid <editor process id>]");
        return 2;
    }
}

await using var connection = new EditorConnection(processId);
var catalog = new ToolCatalogState();
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

var options = new McpServerOptions
{
    ServerInfo = new Implementation { Name = "casa-editor", Version = "1.0.0" },
    Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = true } },
    ServerInstructions =
        "Drives a running CasaEngine editor: read the world, create and edit entities and components, save, capture the view, "
        + "start and stop play mode. Each world change is one undoable step in the editor (history_undo). "
        + EditorToolCatalog.DataNotice,
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = async (request, cancellationToken) =>
        {
            await catalog.RefreshAsync(connection, cancellationToken);
            return new ListToolsResult { Tools = catalog.Tools };
        },
        CallToolHandler = async (request, cancellationToken) =>
        {
            string toolName = request.Params?.Name ?? string.Empty;
            if (toolName == EditorToolCatalog.StatusToolName)
            {
                await catalog.RefreshAsync(connection, cancellationToken);
                return catalog.IsConnected
                    ? new CallToolResult { Content = new List<ContentBlock> { new TextContentBlock { Text = "An editor serves remote control: its tools are listed." } } }
                    : EditorToolCatalog.Failure(EditorToolCatalog.NoEditorMessage);
            }

            string? command = catalog.FindCommand(toolName);
            if (command == null)
            {
                await catalog.RefreshAsync(connection, cancellationToken);
                command = catalog.FindCommand(toolName);
            }

            if (command == null)
            {
                return EditorToolCatalog.Failure(catalog.IsConnected
                    ? $"Unknown tool '{toolName}'."
                    : EditorToolCatalog.NoEditorMessage);
            }

            var (result, error) = await connection.CallAsync(command, EditorToolCatalog.ToParams(request.Params?.Arguments), cancellationToken);
            return result.HasValue ? EditorToolCatalog.Success(result.Value) : EditorToolCatalog.Failure(error ?? "The editor did not answer.");
        },
    },
};

await using var transport = new StdioServerTransport("casa-editor", NullLoggerFactory.Instance);
await using McpServer server = McpServer.Create(transport, options, NullLoggerFactory.Instance, null);

// The editor may start, stop or gain commands while the client is connected: tell the client to list the tools again.
_ = Task.Run(async () =>
{
    while (!stop.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stop.Token);
            if (await catalog.RefreshAsync(connection, stop.Token))
            {
                await server.SendNotificationAsync(NotificationMethods.ToolListChangedNotification, stop.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // The client is not ready for notifications yet, or has gone: try again later.
        }
    }
});

await server.RunAsync(stop.Token);
stop.Cancel();
return 0;

/// <summary>The current tool list and its command names, rebuilt from the editor.</summary>
internal sealed class ToolCatalogState
{
    private readonly object _lock = new();
    private Dictionary<string, string> _commandByTool = new(StringComparer.Ordinal);
    private string _signature = string.Empty;

    public List<Tool> Tools { get; private set; } = new() { EditorToolCatalog.StatusTool() };

    public bool IsConnected { get; private set; }

    public string? FindCommand(string toolName)
    {
        lock (_lock)
        {
            return _commandByTool.TryGetValue(toolName, out string? command) ? command : null;
        }
    }

    /// <summary>Reads the editor's commands; true when the tool list changed.</summary>
    public async Task<bool> RefreshAsync(EditorConnection connection, CancellationToken cancellationToken)
    {
        var (commands, _) = await connection.ListCommandsAsync(cancellationToken);
        List<Tool> tools;
        Dictionary<string, string> commandByTool;
        if (commands == null)
        {
            tools = new List<Tool> { EditorToolCatalog.StatusTool() };
            commandByTool = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        else
        {
            (tools, commandByTool) = EditorToolCatalog.Build(commands);
        }

        string signature = string.Join('|', tools.Select(t => t.Name));
        lock (_lock)
        {
            bool changed = signature != _signature;
            _signature = signature;
            Tools = tools;
            _commandByTool = commandByTool;
            IsConnected = commands != null;
            return changed;
        }
    }
}
