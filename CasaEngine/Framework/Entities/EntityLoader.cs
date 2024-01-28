using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement;

namespace CasaEngine.Framework.Entities;

public class EntityLoader
{
    public static AActor Load(JsonElement element)
    {
        var entity = new AActor();
        entity.Load(element);
        return entity;
    }

    //TODO : why do not use ElementFactory ???
    public static void LoadFromEntityReference(EntityReference entityReference, AssetContentManager assetContentManager)
    {
        if (entityReference.AssetId != Guid.Empty)
        {
            var actor = assetContentManager.Load<AActor>(entityReference.AssetId);
            var entity = new AActor(actor);
            entityReference.Entity = entity;
            entity.Name = entityReference.Name;
            System.Diagnostics.Debugger.Break();
            //entity.RootComponent?.Coordinates = new Coordinates(entityReference.InitialCoordinates);
        }
    }
}