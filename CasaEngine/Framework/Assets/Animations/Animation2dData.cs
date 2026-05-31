using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : AnimationData
{
    public List<FrameData> Frames { get; } = new();
    public List<Animation2dPartData> Parts { get; } = new();
    public List<Animation2dTrackData> Tracks { get; } = new();

    public List<string> GetInvalidTrackTargetPartIds()
    {
        var partIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            partIds.Add(part.Id);
        }

        var invalidPartIds = new List<string>();
        foreach (var track in Tracks)
        {
            if (partIds.Contains(track.TargetPartId) || invalidPartIds.Contains(track.TargetPartId))
            {
                continue;
            }

            invalidPartIds.Add(track.TargetPartId);
        }

        return invalidPartIds;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        foreach (var frameNode in element["frames"])
        {
            var frameData = new FrameData();
            frameData.Load((JObject)frameNode);
            Frames.Add(frameData);
        }
    }

}