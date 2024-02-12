
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class AnimatedTileData : TileData
{
    public string Animation2dId;

    public AnimatedTileData() : base(TileType.Animated)
    { }

    public virtual void Load(JObject jObject)
    {
        base.Load(jObject);
        Animation2dId = jObject["animation_2d_id"].GetString();
    }
}