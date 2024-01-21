using System.Text.Json;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
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

    public static void LoadFromEntityReference(EntityReference entityReference, AssetContentManager assetContentManager)
    {
        if (entityReference.AssetId != Guid.Empty)
        {
            var assetInfo = GameSettings.AssetInfoManager.Get(entityReference.AssetId);
            var assetFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);

            var actor = assetContentManager.Load<AActor>(assetInfo);
            var entity = new AActor(actor);
            entityReference.Entity = entity;
            entity.Name = entityReference.Name;
            System.Diagnostics.Debugger.Break();
            //entity.RootComponent?.Coordinates = new Coordinates(entityReference.InitialCoordinates);
        }
    }
}