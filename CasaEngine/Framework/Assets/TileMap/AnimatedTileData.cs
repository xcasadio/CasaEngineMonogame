using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Assets.TileMap;

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