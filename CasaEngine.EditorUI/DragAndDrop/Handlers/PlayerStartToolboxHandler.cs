using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.DragAndDrop.Handlers;

/// <summary>
/// Handles the toolbox "PlayerStart" drop type: creates an <see cref="Entity"/>
/// with a root <see cref="PlayerStartComponent"/>.
/// </summary>
public class PlayerStartToolboxHandler : IToolboxDropHandler
{
    public string SupportedType => DragAndDropInfoType.PlayerStart;

    public bool CanHandle(DragAndDropInfo info) => true;

    public Entity CreateEntity(DragAndDropInfo info)
    {
        var entity = new Entity();
        entity.RootComponent = new PlayerStartComponent();
        return entity;
    }
}
