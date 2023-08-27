using CasaEngine.Core.Design;
using System.Text.Json;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities;

public class EntityLoader
{
    public static Entity Load(string fileName)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        return Load(jsonDocument.RootElement, SaveOption.Editor);
    }

    public static Entity Load(JsonElement element, SaveOption option)
    {
        var entity = new Entity();
        entity.Load(element, option);
        return entity;
    }

    public static void LoadFromEntityReference(EntityReference entityReference, AssetContentManager assetContentManager,
        GraphicsDevice graphicsDevice)
    {
        if (entityReference.AssetId != IdManager.InvalidId)
        {
            var assetInfo = GameSettings.AssetInfoManager.Get(entityReference.AssetId);
            var assetFileName = Path.Combine(EngineEnvironment.ProjectPath, assetInfo.FileName);
            var entity = assetContentManager.Load<Entity>(assetInfo, graphicsDevice).Clone();
            entityReference.Entity = entity;
            entity.Name = entityReference.Name;
            entity.Coordinates.CopyFrom(entityReference.InitialCoordinates);
        }
    }

    public static List<Entity> LoadFromArray(JsonElement element, SaveOption option)
    {
        var entities = new List<Entity>();

        foreach (var item in element.EnumerateArray())
        {
            entities.Add(Load(item, option));
        }

        return entities;
    }
}