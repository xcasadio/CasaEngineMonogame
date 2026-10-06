using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
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
    private float _referenceDistance = 1f;
    private float _maxDistance = float.MaxValue;
    private float _rolloffFactor = 1f;
    private float _dopplerFactor;
    private IReadOnlyList<AudioParameterBinding> _parameterBindings = Array.Empty<AudioParameterBinding>();

    /// <summary>Highest voice priority an asset can carry.</summary>
    public const int MaxPriority = 100;

    /// <summary>Most parameter bindings an asset keeps; the extra ones are dropped with a warning.</summary>
    public const int MaxParameterBindings = 8;

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

    /// <summary>
    /// How the sound is spatialized when it is played at a position. <see cref="AudioSpatialMode.None"/> (the default)
    /// plays it without spatialization.
    /// </summary>
    public AudioSpatialMode SpatialMode { get; set; } = AudioSpatialMode.None;

    /// <summary>
    /// Distance attenuation model of a spatial sound. The default is <see cref="AudioDistanceModel.InverseDistanceClamped"/>,
    /// the default of the audio specification.
    /// </summary>
    public AudioDistanceModel DistanceModel { get; set; } = AudioDistanceModel.InverseDistanceClamped;

    /// <summary>
    /// Distance, in world units, under which a spatial sound is not attenuated. At least 0, default 1. A NaN or negative
    /// value keeps the previous one. There is no fixed world unit: the value only means something against the positions
    /// given to the audio service.
    /// </summary>
    public float ReferenceDistance
    {
        get => _referenceDistance;
        set => _referenceDistance = SanitizeNonNegative(value, _referenceDistance);
    }

    /// <summary>
    /// Distance, in world units, beyond which the attenuation no longer changes. At least 0, default
    /// <see cref="float.MaxValue"/> (no limit); +infinity is stored as <see cref="float.MaxValue"/>. A NaN or negative
    /// value keeps the previous one.
    /// </summary>
    public float MaxDistance
    {
        get => _maxDistance;
        set => _maxDistance = SanitizeNonNegative(value, _maxDistance);
    }

    /// <summary>How fast the sound fades with the distance. At least 0, default 1. A NaN or negative value keeps the previous one.</summary>
    public float RolloffFactor
    {
        get => _rolloffFactor;
        set => _rolloffFactor = SanitizeNonNegative(value, _rolloffFactor);
    }

    /// <summary>
    /// Strength of the Doppler pitch shift. At least 0, default 0 (Doppler off), 1 is the physical effect. A NaN or
    /// negative value keeps the previous one.
    /// </summary>
    public float DopplerFactor
    {
        get => _dopplerFactor;
        set => _dopplerFactor = SanitizeNonNegative(value, _dopplerFactor);
    }

    /// <summary>
    /// Bindings of game parameters to the volume or the pitch of the voice, at most <see cref="MaxParameterBindings"/>.
    /// Replaced as a whole through <see cref="SetParameterBindings"/>.
    /// </summary>
    public IReadOnlyList<AudioParameterBinding> ParameterBindings => _parameterBindings;

    /// <summary>
    /// Replaces the parameter bindings. Null entries are skipped; beyond <see cref="MaxParameterBindings"/> the extra
    /// bindings are dropped with one warning. Meant for the loader and for tests: it allocates.
    /// </summary>
    public void SetParameterBindings(IEnumerable<AudioParameterBinding> bindings)
    {
        var kept = new List<AudioParameterBinding>();
        var dropped = false;

        if (bindings != null)
        {
            foreach (var binding in bindings)
            {
                if (binding == null)
                {
                    continue;
                }

                if (kept.Count >= MaxParameterBindings)
                {
                    dropped = true;
                    break;
                }

                kept.Add(binding);
            }
        }

        if (dropped)
        {
            Logs.WriteWarning($"Sound asset '{Name}': at most {MaxParameterBindings} parameter bindings are kept, the others are dropped.");
        }

        _parameterBindings = kept.Count == 0 ? Array.Empty<AudioParameterBinding>() : kept;
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

        SpatialMode = ReadEnum(element, "spatial_mode", AudioSpatialMode.None);
        DistanceModel = ReadEnum(element, "distance_model", AudioDistanceModel.InverseDistanceClamped);
        ReferenceDistance = ReadNumber(element, "reference_distance", 1f);
        MaxDistance = ReadNumber(element, "max_distance", float.MaxValue);
        RolloffFactor = ReadNumber(element, "rolloff_factor", 1f);
        DopplerFactor = ReadNumber(element, "doppler_factor", 0f);
        LoadParameterBindings(element);
    }

    /// <summary>
    /// Reads an enumeration written by name, case-insensitive. An unknown name, a numeric string or a non-string token
    /// keeps the default and warns.
    /// </summary>
    private T ReadEnum<T>(JObject element, string key, T defaultValue) where T : struct, Enum
    {
        if (!element.TryGetValue(key, out var token))
        {
            return defaultValue;
        }

        if (token.Type == JTokenType.String
            && Enum.TryParse((string)token, ignoreCase: true, out T value)
            && Enum.IsDefined(value))
        {
            return value;
        }

        Logs.WriteWarning($"Sound asset '{Name}': key '{key}' is not a known {typeof(T).Name} name, the default is used.");
        return defaultValue;
    }

    private void LoadParameterBindings(JObject element)
    {
        var bindings = new List<AudioParameterBinding>();

        if (element.TryGetValue("parameter_bindings", out var token))
        {
            if (token is not JArray array)
            {
                Logs.WriteWarning($"Sound asset '{Name}': key 'parameter_bindings' is not an array, it is ignored.");
            }
            else
            {
                foreach (var entry in array)
                {
                    if (TryReadParameterBinding(entry, out var binding))
                    {
                        bindings.Add(binding);
                    }
                    else
                    {
                        Logs.WriteWarning($"Sound asset '{Name}': an entry of 'parameter_bindings' is invalid, it is ignored.");
                    }
                }
            }
        }

        SetParameterBindings(bindings);
    }

    private static bool TryReadParameterBinding(JToken entry, out AudioParameterBinding binding)
    {
        binding = null;

        if (entry is not JObject node
            || !node.TryGetValue("parameter", out var nameToken)
            || nameToken.Type != JTokenType.String
            || string.IsNullOrWhiteSpace((string)nameToken)
            || !node.TryGetValue("target", out var targetToken)
            || targetToken.Type != JTokenType.String
            || !Enum.TryParse((string)targetToken, ignoreCase: true, out AudioParameterTarget target)
            || !Enum.IsDefined(target)
            || !TryReadFiniteNumber(node, "input_min", out var inputMin)
            || !TryReadFiniteNumber(node, "input_max", out var inputMax)
            || !TryReadFiniteNumber(node, "output_min", out var outputMin)
            || !TryReadFiniteNumber(node, "output_max", out var outputMax))
        {
            return false;
        }

        binding = new AudioParameterBinding((string)nameToken, target, inputMin, inputMax, outputMin, outputMax);
        return true;
    }

    private static bool TryReadFiniteNumber(JObject node, string key, out float value)
    {
        value = 0f;

        if (!node.TryGetValue(key, out var token)
            || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
        {
            return false;
        }

        value = (float)token;
        return float.IsFinite(value);
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

    private static float SanitizeNonNegative(float value, float fallback)
    {
        if (float.IsNaN(value) || value < 0f)
        {
            return fallback;
        }

        return Math.Min(value, float.MaxValue);
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
