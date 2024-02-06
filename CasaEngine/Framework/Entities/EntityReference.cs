using System.Text.Json;
using CasaEngine.Core.Maths;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

public class EntityReference
{
    private JsonElement? _nodeToLoad;

    //if id = Guid.Empty => no reference, the world save the entire entity
    public Guid AssetId { get; set; } = Guid.Empty;
    public string Name { get; set; }
    public Coordinates InitialCoordinates { get; } = new();
    public Entity Entity { get; internal set; }

    public void Load(JsonElement element)
    {
        AssetId = element.GetProperty("asset_id").GetGuid();

        if (AssetId == Guid.Empty)
        {
            _nodeToLoad = element.GetProperty("entity");
        }
        else
        {
            Name = element.GetProperty("name").GetString();
            InitialCoordinates.Load(element.GetProperty("initial_coordinates"));
        }
    }

    public void Load(AssetContentManager assetContentManager)
    {
        if (AssetId == Guid.Empty)
        {
            Entity = assetContentManager.Load<Entity>(_nodeToLoad.Value);
        }
        else
        {
            Entity = assetContentManager.Load<Entity>(AssetId).Clone();
            Entity.RootComponent?.CopyCoordinatesFrom(InitialCoordinates);
        }
    }

#if EDITOR

    public static EntityReference CreateFromAssetInfo(AssetInfo assetInfo, AssetContentManager assetContentManager)
    {
        var entityReference = new EntityReference();
        entityReference.AssetId = assetInfo.Id;
        entityReference.Name = assetInfo.Name;
        entityReference.Entity = assetContentManager.Load<Entity>(assetInfo.Id).Clone();

        if (entityReference.Entity.RootComponent != null)
        {
            entityReference.Entity.RootComponent.CopyCoordinatesFrom(entityReference.InitialCoordinates);
        }

        return entityReference;
    }

    public void Save(JObject jObject)
    {
        jObject.Add("asset_id", AssetId);

        if (AssetId == Guid.Empty)
        {
            var entityNode = new JObject();
            Entity.Save(entityNode);
            jObject.Add("entity", entityNode);
        }
        else
        {
            jObject.Add("name", Name);
            var coordinateNode = new JObject();
            InitialCoordinates.Save(coordinateNode);
            jObject.Add("initial_coordinates", coordinateNode);
        }
    }

#endif
}