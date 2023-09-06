using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

public class EntityReference : ISaveLoad
{
    //if id = InvalidId => no reference, the world save the entire entity
    public long AssetId { get; set; } = IdManager.InvalidId;
    public string Name { get; set; }
    public Coordinates InitialCoordinates { get; internal set; } = new();
    public Entity Entity { get; internal set; }

    public static EntityReference CreateFromAssetInfo(AssetInfo assetInfo, AssetContentManager assetContentManager)
    {
        var entityReference = new EntityReference();
        entityReference.AssetId = assetInfo.Id;
        entityReference.Name = assetInfo.Name;
        entityReference.Entity = assetContentManager.Load<Entity>(assetInfo);
        entityReference.InitialCoordinates.CopyFrom(entityReference.Entity.Coordinates);
        return entityReference;
    }

    public void Load(JsonElement element, SaveOption option)
    {
        AssetId = element.GetProperty("asset_id").GetInt64();

        if (AssetId == IdManager.InvalidId)
        {
            Entity = EntityLoader.Load(element.GetProperty("entity"), option);
        }
        else
        {
            Name = element.GetProperty("name").GetString();
            InitialCoordinates.Load(element.GetProperty("initial_coordinates"));
        }
    }

#if EDITOR

    public void Save(JObject jObject, SaveOption option)
    {
        jObject.Add("asset_id", AssetId);

        if (AssetId == IdManager.InvalidId)
        {
            var entityNode = new JObject();
            Entity.Save(entityNode, option);
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