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
var catalog = new EditorToolCatalogState();
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
                return catalog.LastStatus switch
                {
                    EditorCallStatus.Answered => new CallToolResult { Content = new List<ContentBlock> { new TextContentBlock { Text = "An editor serves remote control: its tools are listed." } } },
                    EditorCallStatus.Busy => EditorToolCatalog.Failure(EditorConnection.BusyMessage),
                    _ => EditorToolCatalog.Failure(EditorToolCatalog.NoEditorMessage),
                };
            }

            string? command = catalog.FindCommand(toolName);
            if (command == null)
            {
                await catalog.RefreshAsync(connection, cancellationToken);
                command = catalog.FindCommand(toolName);
            }

            if (command == null)
            {
                return EditorToolCatalog.Failure(catalog.LastStatus switch
                {
                    EditorCallStatus.Busy => EditorConnection.BusyMessage,
                    EditorCallStatus.NoEditor => EditorToolCatalog.NoEditorMessage,
                    _ => $"Unknown tool '{toolName}'.",
                });
            }

            EditorCallResult call = await connection.CallAsync(command, EditorToolCatalog.ToParams(request.Params?.Arguments), cancellationToken);
            return call.Status == EditorCallStatus.Answered
                ? EditorToolCatalog.Success(call.Result!.Value)
                : EditorToolCatalog.Failure(call.Error ?? "The editor did not answer.");
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

