
using CasaEngine.Core.Math;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities;

public class EntityReference
{
    private JObject? _nodeToLoad;

    //if id = Guid.Empty => no reference, the world save the entire entity
    public Guid AssetId { get; set; } = Guid.Empty;
    public string Name { get; set; }
    public Coordinates InitialCoordinates { get; private set; } = new();
    public Entity Entity { get; internal set; }

    public void Load(JObject element)
    {
        AssetId = element["asset_id"].GetGuid();

        if (AssetId == Guid.Empty)
        {
            _nodeToLoad = (JObject)element["entity"];
        }
        else
        {
            Name = element["name"].GetString();
            InitialCoordinates = element["initial_coordinates"].GetCoordinates();
        }
    }

    public void Load(AssetContentManager assetContentManager)
    {
        if (Entity != null)
        {
            return;
        }

        if (AssetId == Guid.Empty)
        {
            Entity = assetContentManager.Load<Entity>(_nodeToLoad);
        }
        else
        {
            Entity = assetContentManager.Load<Entity>(AssetId).Clone();
            Entity.RootComponent?.CopyCoordinatesFrom(InitialCoordinates);
        }
    }

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

}