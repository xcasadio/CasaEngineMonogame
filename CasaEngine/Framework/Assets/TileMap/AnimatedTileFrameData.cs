using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public sealed class AnimatedTileFrameData
{
    public int TileId { get; set; }
    public int DurationMilliseconds { get; set; }

    public void Load(JObject element)
    {
        TileId = element["tile_id"].GetInt32();
        DurationMilliseconds = element["duration_ms"].GetInt32();
    }
}