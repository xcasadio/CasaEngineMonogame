using CasaEngine.Core.Design;
using System.Text.Json;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;

namespace CasaEngine.Framework.Entities;

public class EntityLoader
{
    public static Entity Load(JsonElement element, SaveOption option)
    {
        var entity = new Entity();
        entity.Load(element, option);
        return entity;
    }

    public static void LoadFromEntityReference(EntityReference entityReference, AssetContentManager assetContentManager)
    {
        if (entityReference.AssetId != IdManager.InvalidId)
        {
            var assetInfo = GameSettings.AssetInfoManager.Get(entityReference.AssetId);
            var assetFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
            var entity = assetContentManager.Load<Entity>(assetInfo).Clone();
            entityReference.Entity = entity;
            entity.Name = entityReference.Name;
            entity.Coordinates.CopyFrom(entityReference.InitialCoordinates);
        }
    }
}