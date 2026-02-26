using System.Collections.Generic;

namespace CasaEngine.EditorUI.DragAndDrop;

/// <summary>
/// Central registry that resolves the correct <see cref="IToolboxDropHandler"/>
/// for a given <see cref="DragAndDropInfo"/>. Use <see cref="Instance"/> to access
/// the application-wide singleton.
/// </summary>
public class ToolboxDropHandlerRegistry
{
    private static ToolboxDropHandlerRegistry? _instance;

    /// <summary>Application-wide singleton instance.</summary>
    public static ToolboxDropHandlerRegistry Instance => _instance ??= new ToolboxDropHandlerRegistry();

    private readonly List<IToolboxDropHandler> _handlers = new();

    private ToolboxDropHandlerRegistry() { }

    /// <summary>Registers a handler into the registry.</summary>
    public void Register(IToolboxDropHandler handler)
    {
        _handlers.Add(handler);
    }

    /// <summary>
    /// Returns the first handler that can process the given <see cref="DragAndDropInfo"/>,
    /// or <c>null</c> if none matches.
    /// </summary>
    public IToolboxDropHandler? FindHandler(DragAndDropInfo info)
    {
        foreach (var handler in _handlers)
        {
            if (handler.SupportedType == info.Type && handler.CanHandle(info))
                return handler;
        }
        return null;
    }
}
