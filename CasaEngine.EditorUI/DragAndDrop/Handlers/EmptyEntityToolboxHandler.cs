using CasaEngine.Framework.Entities;

namespace CasaEngine.EditorUI.DragAndDrop.Handlers;

/// <summary>
/// Handles the toolbox "Entity" drop type: creates a plain empty <see cref="Entity"/>
/// with no root component.
/// </summary>
public class EmptyEntityToolboxHandler : IToolboxDropHandler
{
    public string SupportedType => DragAndDropInfoType.Entity;

    public bool CanHandle(DragAndDropInfo info) => true;

    public Entity CreateEntity(DragAndDropInfo info) => new Entity();
}
