
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.TileMap;

public class AnimatedTileData : TileData
{
    public string Animation2dId;
    public Rectangle Location { get; set; }
    public List<AnimatedTileFrameData> Frames { get; } = new();

    public AnimatedTileData() : base(TileType.Animated)
    { }

    public override void Load(JObject jObject)
    {
        base.Load(jObject);
        Animation2dId = jObject["animation_2d_id"]?.GetString();
        if (jObject["location"] != null)
        {
            Location = jObject["location"].GetRectangle();
        }

        Frames.Clear();

        if (jObject["animation_frames"] is not JArray framesArray)
        {
            return;
        }

        foreach (var frameToken in framesArray)
        {
            if (frameToken is not JObject frameObject)
            {
                continue;
            }

            var frame = new AnimatedTileFrameData();
            frame.Load(frameObject);
            Frames.Add(frame);
        }
    }
}