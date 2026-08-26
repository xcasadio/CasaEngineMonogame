using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Common;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// Authoring asset (<c>.sound</c>) describing how an audio file is played.
/// </summary>
/// <remarks>
/// It references the audio file the same way a <c>.texture</c> references its png: through the
/// asset catalogue, by id. Every field has a neutral default so a freshly created asset is
/// playable, and an incomplete document loads instead of failing.
/// </remarks>
public class SoundAsset : ObjectBase
{
    private float _volume = 1f;
    private float _pitch;
    private string _busName = AudioBusNames.Sfx;

    public SoundAsset()
    {
        Name = $"Sound {Id}";
    }

    /// <summary>Id of the audio file asset (a <c>.wav</c>) in the catalogue.</summary>
    public Guid AudioFileAssetId { get; set; } = Guid.Empty;

    /// <summary>Playback volume in [0,1], before the bus gain. Out of range values are clamped.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = SanitizeVolume(value, _volume);
    }

    /// <summary>Pitch shift in [-1,1] (one octave down to one octave up).</summary>
    public float Pitch
    {
        get => _pitch;
        set => _pitch = SanitizePitch(value, _pitch);
    }

    public bool IsLooped { get; set; }

    /// <summary>Bus this sound is routed to by default. See <see cref="AudioBusNames"/>.</summary>
    public string BusName
    {
        get => _busName;
        set => _busName = string.IsNullOrWhiteSpace(value) ? AudioBusNames.Sfx : value;
    }

    /// <summary>
    /// True for a long sound decoded on the fly (music, ambience), false for a short sound kept
    /// fully in memory. This is authored, not deduced from the extension: the same wav can be
    /// used either way.
    /// </summary>
    public bool IsStreaming { get; set; }

    /// <summary>Playback parameters of this asset, before any per-call override.</summary>
    public AudioVoiceParameters CreateVoiceParameters()
    {
        return new AudioVoiceParameters(Volume, 0f, Pitch, IsLooped);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        AudioFileAssetId = element.ContainsKey("audio_file_asset_id")
            ? element["audio_file_asset_id"].GetGuid()
            : Guid.Empty;

        Volume = element.ContainsKey("volume") ? element["volume"].GetSingle() : 1f;
        Pitch = element.ContainsKey("pitch") ? element["pitch"].GetSingle() : 0f;
        IsLooped = element.ContainsKey("is_looped") && element["is_looped"].GetBoolean();
        BusName = element.ContainsKey("bus_name") ? element["bus_name"].GetString() : AudioBusNames.Sfx;
        IsStreaming = element.ContainsKey("is_streaming") && element["is_streaming"].GetBoolean();
    }

    private static float SanitizeVolume(float value, float fallback)
    {
        if (float.IsNaN(value))
        {
            return fallback;
        }

        return Math.Clamp(value, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
    }

    private static float SanitizePitch(float value, float fallback)
    {
        if (float.IsNaN(value))
        {
            return fallback;
        }

        return Math.Clamp(value, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch);
    }
}
