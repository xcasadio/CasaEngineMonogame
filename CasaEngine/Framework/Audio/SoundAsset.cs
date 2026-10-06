using CasaEngine.Core.Logging;
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
    private int _priority;
    private float _variationVolumeMin = 1f;
    private float _variationVolumeMax = 1f;
    private float _variationPitchMin;
    private float _variationPitchMax;

    /// <summary>Highest voice priority an asset can carry.</summary>
    public const int MaxPriority = 100;

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

    /// <summary>
    /// Voice priority in [0, <see cref="MaxPriority"/>]. 0 means no priority: such a voice is never
    /// stolen and never steals another one. Out of range values are clamped.
    /// </summary>
    public int Priority
    {
        get => _priority;
        set => _priority = Math.Clamp(value, 0, MaxPriority);
    }

    /// <summary>
    /// Extra audio file assets the random draw can pick, besides <see cref="AudioFileAssetId"/>.
    /// <see cref="Guid.Empty"/> entries are tolerated.
    /// </summary>
    public List<Guid> VariationAudioFileAssetIds { get; } = new();

    /// <summary>
    /// Lower end of the random volume factor in [0,1]. The factor only attenuates, relative to the
    /// played volume. Out of range values are clamped; an inverted range is kept as is.
    /// </summary>
    public float VariationVolumeMin
    {
        get => _variationVolumeMin;
        set => _variationVolumeMin = SanitizeVolume(value, _variationVolumeMin);
    }

    /// <summary>Upper end of the random volume factor in [0,1]. See <see cref="VariationVolumeMin"/>.</summary>
    public float VariationVolumeMax
    {
        get => _variationVolumeMax;
        set => _variationVolumeMax = SanitizeVolume(value, _variationVolumeMax);
    }

    /// <summary>
    /// Lower end of the random pitch offset in [-1,1] octaves, added to the played pitch. Out of
    /// range values are clamped; an inverted range is kept as is.
    /// </summary>
    public float VariationPitchMin
    {
        get => _variationPitchMin;
        set => _variationPitchMin = SanitizePitch(value, _variationPitchMin);
    }

    /// <summary>Upper end of the random pitch offset in [-1,1] octaves. See <see cref="VariationPitchMin"/>.</summary>
    public float VariationPitchMax
    {
        get => _variationPitchMax;
        set => _variationPitchMax = SanitizePitch(value, _variationPitchMax);
    }

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

        var priority = ReadNumber(element, "priority", 0f);
        Priority = float.IsNaN(priority) ? 0 : (int)Math.Clamp(priority, 0f, MaxPriority);
        VariationVolumeMin = ReadNumber(element, "variation_volume_min", 1f);
        VariationVolumeMax = ReadNumber(element, "variation_volume_max", 1f);
        VariationPitchMin = ReadNumber(element, "variation_pitch_min", 0f);
        VariationPitchMax = ReadNumber(element, "variation_pitch_max", 0f);
        LoadVariationAudioFileAssetIds(element);
    }

    /// <summary>Reads a numeric key; anything but a JSON number keeps the default and warns.</summary>
    private float ReadNumber(JObject element, string key, float defaultValue)
    {
        if (!element.TryGetValue(key, out var token))
        {
            return defaultValue;
        }

        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
        {
            Logs.WriteWarning($"Sound asset '{Name}': key '{key}' is not a number, the default is used.");
            return defaultValue;
        }

        return (float)token;
    }

    private void LoadVariationAudioFileAssetIds(JObject element)
    {
        VariationAudioFileAssetIds.Clear();

        if (!element.TryGetValue("variation_audio_file_asset_ids", out var token))
        {
            return;
        }

        if (token is not JArray array)
        {
            Logs.WriteWarning($"Sound asset '{Name}': key 'variation_audio_file_asset_ids' is not an array, it is ignored.");
            return;
        }

        foreach (var entry in array)
        {
            if (entry.Type == JTokenType.String && Guid.TryParse((string)entry, out var guid))
            {
                VariationAudioFileAssetIds.Add(guid);
            }
            else
            {
                Logs.WriteWarning($"Sound asset '{Name}': an entry of 'variation_audio_file_asset_ids' is not a valid GUID, it is ignored.");
            }
        }
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
