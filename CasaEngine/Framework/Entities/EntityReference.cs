using System.Text.Json;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.SceneManagement;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities;

public class EntityReference : ISaveLoad
{
    //if id = InvalidId => no reference, the world save the entire entity
    public Guid AssetId { get; set; } = Guid.Empty;
    public string Name { get; set; }
    public Coordinates InitialCoordinates { get; internal set; } = new();
    public AActor Entity { get; internal set; }

    public static EntityReference CreateFromAssetInfo(AssetInfo assetInfo, AssetContentManager assetContentManager)
    {
        var entityReference = new EntityReference();
        System.Diagnostics.Debugger.Break();
        //entityReference.AssetId = assetInfo.Id;
        entityReference.Name = assetInfo.Name;
        entityReference.Entity = assetContentManager.Load<AActor>(assetInfo);
        //entityReference.InitialCoordinates.CopyFrom(entityReference.Entity.Coordinates);
        return entityReference;
    }

    public void Load(JsonElement element, SaveOption option)
    {
        AssetId = element.GetProperty("asset_id").GetGuid();

        if (AssetId == Guid.Empty)
        {
            Entity = EntityLoader.Load(element.GetProperty("entity"));
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