using CasaEngine.Framework.Entities;

namespace CasaEngine.EditorUI.DragAndDrop;

/// <summary>
/// Defines a handler for toolbox items dragged from the editor toolbox panel
/// (carried as a JSON-serialised <see cref="DragAndDropInfo"/>).
/// </summary>
public interface IToolboxDropHandler
{
    /// <summary>
    /// The <see cref="DragAndDropInfo.Type"/> value this handler responds to
    /// (e.g. "Entity", "PlayerStart").
    /// </summary>
    string SupportedType { get; }

    /// <summary>
    /// Returns true if this handler can process the given <see cref="DragAndDropInfo"/>.
    /// </summary>
    bool CanHandle(DragAndDropInfo info);

    /// <summary>
    /// Creates and returns a configured <see cref="Entity"/> for the toolbox item.
    /// The entity is NOT added to the world — the calling control decides placement.
    /// </summary>
    Entity CreateEntity(DragAndDropInfo info);
}
