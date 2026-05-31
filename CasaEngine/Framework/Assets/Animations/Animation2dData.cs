using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dData : AnimationData
{
    public List<Animation2dPartData> Parts { get; } = new();
    public List<Animation2dTrackData> Tracks { get; } = new();
    public List<AnimationEventAsset> Events { get; } = new();

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

        Parts.Clear();
        Tracks.Clear();
        Events.Clear();

        if (element["parts"] is JArray partsNode)
        {
            foreach (var partNode in partsNode)
            {
                if (partNode is JObject partObject)
                {
                    Parts.Add(Animation2dPartData.Load(partObject));
                }
            }
        }

        if (element["tracks"] is JArray tracksNode)
        {
            foreach (var trackNode in tracksNode)
            {
                if (trackNode is JObject trackObject)
                {
                    Tracks.Add(Animation2dTrackData.Load(trackObject));
                }
            }
        }

        if (element["events"] is not JArray eventsNode)
        {
            return;
        }

        foreach (var eventNode in eventsNode)
        {
            if (eventNode is JObject eventObject)
            {
                Events.Add(AnimationEventAssetJsonSerializer.Load(eventObject));
            }
        }
    }

}