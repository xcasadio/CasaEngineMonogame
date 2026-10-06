using CasaEngine.Framework.Common;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// Authoring asset (<c>.audioMixer</c>): the buses, insert effects and sends a project adds to the mixer.
/// One per project. The Master bus is not part of it.
/// </summary>
public sealed class AudioMixerAsset : ObjectBase
{
    public const int CurrentVersion = 1;

    public AudioMixerAsset()
    {
        Name = $"Audio mixer {Id}";
    }

    public int Version { get; set; } = CurrentVersion;

    public List<AudioMixerBusData> Buses { get; } = new();

    /// <summary>An asset holding Music, Sfx, Voice and Ui under Master at volume 1.</summary>
    public static AudioMixerAsset CreateDefault(string name)
    {
        var asset = new AudioMixerAsset();
        if (!string.IsNullOrWhiteSpace(name))
        {
            asset.Name = name;
        }

        string[] busNames = { AudioBusNames.Music, AudioBusNames.Sfx, AudioBusNames.Voice, AudioBusNames.Ui };
        for (int index = 0; index < busNames.Length; index++)
        {
            asset.Buses.Add(new AudioMixerBusData { Name = busNames[index], Parent = AudioBusNames.Master, Volume = 1f });
        }

        return asset;
    }

    public override void Load(JObject element)
    {
        base.Load(element);
        AudioMixerAssetJsonSerializer.Load(this, element);
    }
}
