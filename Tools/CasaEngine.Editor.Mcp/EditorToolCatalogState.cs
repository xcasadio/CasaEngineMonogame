using CasaEngine.Editor.Remote.Protocol;
using ModelContextProtocol.Protocol;

namespace CasaEngine.Editor.Mcp;

/// <summary>
/// The MCP server's current tool list and the command each tool calls, rebuilt from the editor. A busy editor keeps the
/// last list (ADR-0093 P2): only an absent editor reduces the list to <see cref="EditorToolCatalog.StatusToolName"/>.
/// </summary>
public sealed class EditorToolCatalogState
{
    private readonly object _lock = new();
    private Dictionary<string, string> _commandByTool = new(StringComparer.Ordinal);
    private List<Tool> _tools = new() { EditorToolCatalog.StatusTool() };
    private string _signature = EditorToolCatalog.StatusToolName;

    public IList<Tool> Tools
    {
        get
        {
            lock (_lock)
            {
                return _tools;
            }
        }
    }

    /// <summary>Status of the last refresh.</summary>
    public EditorCallStatus LastStatus { get; private set; } = EditorCallStatus.NoEditor;

    /// <summary>True while the list holds the editor's commands (connected, or busy since).</summary>
    public bool HasEditorTools { get; private set; }

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
        var (commands, call) = await connection.ListCommandsAsync(cancellationToken);
        return Apply(commands, call.Status);
    }

    /// <summary>Applies a refresh outcome (separate from the connection for the tests).</summary>
    public bool Apply(IReadOnlyList<RemoteCommandDescriptor>? commands, EditorCallStatus status)
    {
        List<Tool> tools;
        Dictionary<string, string> commandByTool;
        if (commands != null)
        {
            (tools, commandByTool) = EditorToolCatalog.Build(commands);
        }
        else if (status == EditorCallStatus.Busy || status == EditorCallStatus.Failed)
        {
            // Busy or a broken call: the editor is still there, keep what it offered.
            LastStatus = status;
            return false;
        }
        else
        {
            tools = new List<Tool> { EditorToolCatalog.StatusTool() };
            commandByTool = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        string signature = string.Join('|', tools.Select(t => t.Name));
        lock (_lock)
        {
            bool changed = signature != _signature;
            _signature = signature;
            _tools = tools;
            _commandByTool = commandByTool;
            LastStatus = status;
            HasEditorTools = commands != null;
            return changed;
        }
    }
}
