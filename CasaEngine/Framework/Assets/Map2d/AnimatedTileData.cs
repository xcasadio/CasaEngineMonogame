using CasaEngine.Core.Helpers;
using System.Text.Json;

namespace CasaEngine.Framework.Assets.Map2d;

public class AnimatedTileData : TileData
{
    public string Animation2dId;

    public AnimatedTileData() : base(TileType.Animated)
    { }

    public virtual void Load(JsonElement jObject)
    {
        base.Load(jObject);
        Animation2dId = jObject.GetJsonPropertyByName("animation_2d_id").Value.GetString();
    }
}