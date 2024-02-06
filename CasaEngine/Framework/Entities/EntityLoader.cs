using System.Text.Json;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Entities;

public static class EntityLoader
{
    public static Entity Load(JsonElement element)
    {
        var entity = new Entity();
        entity.Load(element);
        return entity;
    }

    //TODO : why do not use ElementFactory ???
    public static void LoadFromEntityReference(EntityReference entityReference, AssetContentManager assetContentManager)
    {
        if (entityReference.AssetId == Guid.Empty)
        {
            return;
        }

        var entity = assetContentManager.Load<Entity>(entityReference.AssetId).Clone();
        entityReference.Entity = entity;
        entity.Name = entityReference.Name;
        entity.RootComponent?.CopyCoordinatesFrom(entityReference.InitialCoordinates);
    }
}