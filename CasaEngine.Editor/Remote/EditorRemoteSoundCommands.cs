using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Spatial;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// Sound commands (ADR-0093, plan T2.5). A sound has no undo history, like its inspector: <c>sound.set_property</c>
/// marks the panel modified and <c>sound.save</c> writes it. Properties use the names of the <c>.sound</c> file and
/// typed JSON values, inside the bounds of the inspector fields (plan P5).
/// </summary>
internal static class EditorRemoteSoundCommands
{
    /// <summary>Most variation files one set writes.</summary>
    public const int MaxVariationFiles = 64;

    /// <summary>The upper bound of the distance fields of the inspector.</summary>
    public const float MaxDistanceRange = 100000f;

    /// <summary>The upper bound of the rolloff and Doppler fields of the inspector.</summary>
    public const float MaxFactorRange = 10f;

    private const string AssetArgument = "{\"type\":\"string\",\"description\":\"Sound asset id (GUID) or project-relative .sound file.\"}";

    private enum ValueKind
    {
        Number,
        Integer,
        Boolean,
        Text,
        AudioFile,
        AudioFiles,
        Bus,
        SpatialMode,
        DistanceModel,
    }

    private sealed record Key(string Name, ValueKind Kind, float Min, float Max, Action<SoundAsset, JsonElement> Apply);

    private static readonly Key[] Keys =
    {
        new("audio_file_asset_id", ValueKind.AudioFile, 0f, 0f, (s, v) => s.AudioFileAssetId = ReadGuid(v)),
        new("variation_audio_file_asset_ids", ValueKind.AudioFiles, 0f, 0f, ApplyVariations),
        new("volume", ValueKind.Number, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume, (s, v) => s.Volume = v.GetSingle()),
        new("pitch", ValueKind.Number, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch, (s, v) => s.Pitch = v.GetSingle()),
        new("variation_volume_min", ValueKind.Number, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume, (s, v) => s.VariationVolumeMin = v.GetSingle()),
        new("variation_volume_max", ValueKind.Number, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume, (s, v) => s.VariationVolumeMax = v.GetSingle()),
        new("variation_pitch_min", ValueKind.Number, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch, (s, v) => s.VariationPitchMin = v.GetSingle()),
        new("variation_pitch_max", ValueKind.Number, AudioVoiceParameters.MinPitch, AudioVoiceParameters.MaxPitch, (s, v) => s.VariationPitchMax = v.GetSingle()),
        new("priority", ValueKind.Integer, 0f, SoundAsset.MaxPriority, (s, v) => s.Priority = v.GetInt32()),
        new("exclusion_group", ValueKind.Text, 0f, 0f, (s, v) => s.ExclusionGroup = v.GetString()),
        new("exclusion_fade_seconds", ValueKind.Number, 0f, SoundAsset.MaxExclusionFadeSeconds, (s, v) => s.ExclusionFadeSeconds = v.GetSingle()),
        new("is_looped", ValueKind.Boolean, 0f, 0f, (s, v) => s.IsLooped = v.GetBoolean()),
        new("is_streaming", ValueKind.Boolean, 0f, 0f, (s, v) => s.IsStreaming = v.GetBoolean()),
        new("bus_name", ValueKind.Bus, 0f, 0f, (s, v) => s.BusName = v.GetString()),
        new("spatial_mode", ValueKind.SpatialMode, 0f, 0f, (s, v) => s.SpatialMode = Enum.Parse<AudioSpatialMode>(v.GetString(), ignoreCase: true)),
        new("distance_model", ValueKind.DistanceModel, 0f, 0f, (s, v) => s.DistanceModel = Enum.Parse<AudioDistanceModel>(v.GetString(), ignoreCase: true)),
        new("reference_distance", ValueKind.Number, 0f, MaxDistanceRange, (s, v) => s.ReferenceDistance = v.GetSingle()),
        new("max_distance", ValueKind.Number, 0f, MaxDistanceRange, (s, v) => s.MaxDistance = v.GetSingle()),
        new("rolloff_factor", ValueKind.Number, 0f, MaxFactorRange, (s, v) => s.RolloffFactor = v.GetSingle()),
        new("doppler_factor", ValueKind.Number, 0f, MaxFactorRange, (s, v) => s.DopplerFactor = v.GetSingle()),
    };

    /// <summary>The property names <c>sound.set_property</c> accepts.</summary>
    public static IEnumerable<string> KeyNames
    {
        get
        {
            foreach (Key key in Keys)
            {
                yield return key.Name;
            }
        }
    }

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host)
    {
        registry.Register(new EditorRemoteCommand(
            "sound.create",
            "Creates <folder>/<name>.sound (default settings, no audio file) and adds it to the asset catalog. Never overwrites a file or reuses a catalogued name.",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"folder\":{\"type\":\"string\",\"description\":\"Existing project folder, '' for the root.\"}},\"required\":[\"name\",\"folder\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteNewAssetFile file = EditorRemoteAssetCreation.Prepare(host.ProjectDirectory, call.GetString("folder", 260), call.GetString("name", 64), EditorRemoteAssetTypes.Sound);
                if (!host.CreateSoundAsset(file, out Guid assetId, out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The sound was not created.");
                }

                call.Succeed(new { asset_id = assetId, file = file.RelativeFile.Replace('\\', '/') });
            }));

        registry.Register(new EditorRemoteCommand(
            "sound.get",
            "Opens a sound in its panel and reads its settings (the names of the .sound file), whether the panel has unsaved changes, and the settable properties.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: true,
            destructive: false,
            call =>
            {
                RemoteAssetReference asset = OpenSound(call, host);
                SoundAsset sound = host.GetSoundAsset(asset.Info.Id)
                    ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' has no sound loaded: see logs.read.");
                call.Succeed(new
                {
                    asset_id = asset.Info.Id,
                    audio_file_asset_id = sound.AudioFileAssetId,
                    variation_audio_file_asset_ids = sound.VariationAudioFileAssetIds,
                    volume = sound.Volume,
                    pitch = sound.Pitch,
                    variation_volume_min = sound.VariationVolumeMin,
                    variation_volume_max = sound.VariationVolumeMax,
                    variation_pitch_min = sound.VariationPitchMin,
                    variation_pitch_max = sound.VariationPitchMax,
                    priority = sound.Priority,
                    exclusion_group = sound.ExclusionGroup,
                    exclusion_fade_seconds = sound.ExclusionFadeSeconds,
                    is_looped = sound.IsLooped,
                    is_streaming = sound.IsStreaming,
                    bus_name = sound.BusName,
                    spatial_mode = sound.SpatialMode.ToString(),
                    distance_model = sound.DistanceModel.ToString(),
                    reference_distance = sound.ReferenceDistance,
                    max_distance = sound.MaxDistance == float.MaxValue ? (float?)null : sound.MaxDistance,
                    max_distance_unlimited = sound.MaxDistance == float.MaxValue,
                    rolloff_factor = sound.RolloffFactor,
                    doppler_factor = sound.DopplerFactor,
                    modified = host.IsSoundModified(asset.Info.Id),
                    keys = KeyNames,
                });
            }));

        registry.Register(new EditorRemoteCommand(
            "sound.set_property",
            "Sets one sound setting through its panel. NOT undoable (sounds have no history): the panel is marked modified until sound.save. " +
            "property = a name of the .sound file (see sound.get keys); value = a typed JSON value inside the inspector bounds: numbers, " +
            "integers, true/false, a .wav/.ogg asset id of the catalog (or the empty GUID), an array of them, a bus of the project, an enum name.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"property\":{\"type\":\"string\"},\"value\":{}},\"required\":[\"asset\",\"property\",\"value\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteAssetReference asset = OpenSound(call, host);
                string property = call.GetString("property", 128);
                JsonElement value = call.GetArgument("value");
                string error = Validate(property, value);
                if (error != null)
                {
                    throw new EditorRemoteArgumentException(error);
                }

                Key key = Find(property);
                if (!host.TrySetSoundProperty(asset.Info.Id, sound => key.Apply(sound, value), out error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The sound was not changed.");
                }

                call.Succeed(new { asset_id = asset.Info.Id, property = key.Name, undoable = false });
            }));

        registry.Register(new EditorRemoteCommand(
            "sound.save",
            "Saves one sound from its open panel.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RemoteAssetReference asset = OpenSound(call, host);
                if (!host.TrySaveAsset(asset.Info.Id, out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The sound was not saved.");
                }

                call.Succeed(new { saved = true });
            }));
    }

    /// <summary>
    /// The bounds of plan P5 for a sound property (security review S2-2): a known key, the JSON type of the key, finite
    /// numbers inside the inspector field bounds, enums by defined name, audio files of the catalog (.wav or .ogg), a bus
    /// the inspector lists, text of 4096 characters at most. Null when allowed, otherwise the reason.
    /// </summary>
    public static string Validate(string property, JsonElement value)
    {
        Key key = Find(property);
        if (key == null)
        {
            return $"'{property}' is not a sound property: one of {string.Join(", ", KeyNames)}.";
        }

        switch (key.Kind)
        {
            case ValueKind.Number:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out float number) || !float.IsFinite(number))
                {
                    return $"'{key.Name}' expects a finite number.";
                }

                return number < key.Min || number > key.Max ? $"'{key.Name}' must be within [{key.Min}, {key.Max}]." : null;
            case ValueKind.Integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int integer))
                {
                    return $"'{key.Name}' expects an integer.";
                }

                return integer < key.Min || integer > key.Max ? $"'{key.Name}' must be within [{key.Min}, {key.Max}]." : null;
            case ValueKind.Boolean:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : $"'{key.Name}' expects true or false.";
            case ValueKind.Text:
                return value.ValueKind == JsonValueKind.String && value.GetString().Length <= EditorRemoteValidation.MaxPropertyTextLength
                    ? null
                    : $"'{key.Name}' expects a text of {EditorRemoteValidation.MaxPropertyTextLength} characters at most.";
            case ValueKind.AudioFile:
                return CheckAudioFile(key.Name, value);
            case ValueKind.AudioFiles:
                if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > MaxVariationFiles)
                {
                    return $"'{key.Name}' expects an array of {MaxVariationFiles} audio file asset ids at most.";
                }

                foreach (JsonElement item in value.EnumerateArray())
                {
                    string error = CheckAudioFile(key.Name, item);
                    if (error != null)
                    {
                        return error;
                    }
                }

                return null;
            case ValueKind.Bus:
                return CheckBus(value);
            case ValueKind.SpatialMode:
                return CheckEnum<AudioSpatialMode>(key.Name, value);
            case ValueKind.DistanceModel:
                return CheckEnum<AudioDistanceModel>(key.Name, value);
            default:
                return $"'{key.Name}' cannot be set.";
        }
    }

    private static Key Find(string property)
    {
        foreach (Key key in Keys)
        {
            if (string.Equals(key.Name, property, StringComparison.Ordinal))
            {
                return key;
            }
        }

        return null;
    }

    private static string CheckAudioFile(string name, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out Guid id))
        {
            return $"'{name}' expects audio file asset ids.";
        }

        if (id == Guid.Empty)
        {
            return null;
        }

        // The formats the inspector's selector offers: the ones the engine decodes.
        AssetInfo info = AssetCatalog.Get(id);
        return info != null
            && (info.FileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || info.FileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            ? null
            : $"{id} is not a .wav or .ogg asset of the catalog.";
    }

    private static string CheckBus(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return "'bus_name' expects a bus name.";
        }

        string bus = value.GetString();
        SoundBusChoices.TryLoadProjectMixer(out var projectMixer);
        foreach (SoundBusChoice choice in SoundBusChoices.Resolve(projectMixer, null))
        {
            if (string.Equals(choice.Name, bus, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return $"'{bus}' is not a bus the sound inspector lists.";
    }

    private static string CheckEnum<T>(string name, JsonElement value) where T : struct, Enum
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            foreach (string defined in Enum.GetNames<T>())
            {
                if (string.Equals(defined, value.GetString(), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
        }

        return $"'{name}' expects one of {string.Join(", ", Enum.GetNames<T>())}.";
    }

    private static Guid ReadGuid(JsonElement value) => Guid.Parse(value.GetString());

    private static void ApplyVariations(SoundAsset sound, JsonElement value)
    {
        sound.VariationAudioFileAssetIds.Clear();
        foreach (JsonElement item in value.EnumerateArray())
        {
            sound.VariationAudioFileAssetIds.Add(ReadGuid(item));
        }
    }

    private static RemoteAssetReference OpenSound(EditorRemoteCall call, IEditorRemoteHost host)
    {
        RemoteAssetReference asset = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("asset", 260), EditorRemoteAssetTypes.Sound);
        if (host.OpenAsset(asset.FullPath, asset.Type) == null)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' could not be opened: see logs.read.");
        }

        return asset;
    }

    private static void RequireEditable(IEditorRemoteHost host)
    {
        if (host.IsPlaySessionActive)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "The editor is in play mode: call play.stop first.");
        }
    }
}
