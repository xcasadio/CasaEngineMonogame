using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Audio.Effects;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>JSON keys of the <c>.audioMixer</c> document, shared by the loader and the editor writer.</summary>
internal static class AudioMixerAssetKeys
{
    public const string Id = "id";
    public const string Name = "name";
    public const string Type = "type";
    public const string Version = "version";
    public const string SchemaVersion = "schema_version";
    public const string Buses = "buses";
    public const string Parent = "parent";
    public const string Volume = "volume";
    public const string Effects = "effects";
    public const string Sends = "sends";
    public const string Target = "target";
    public const string Level = "level";

    public const string BiquadType = "biquad";
    public const string CompressorType = "compressor";
    public const string ReverbType = "reverb";
    public const string DuckingType = "ducking";

    public const string Filter = "filter";
    public const string FrequencyHz = "frequency_hz";
    public const string Q = "q";
    public const string GainDb = "gain_db";

    public const string ThresholdDb = "threshold_db";
    public const string Ratio = "ratio";
    public const string KneeDb = "knee_db";
    public const string AttackSeconds = "attack_seconds";
    public const string ReleaseSeconds = "release_seconds";
    public const string MakeupGainDb = "makeup_gain_db";

    public const string RoomSize = "room_size";
    public const string Damping = "damping";
    public const string Wet = "wet";
    public const string Dry = "dry";
    public const string StereoSeparation = "stereo_separation";

    public const string Source = "source";
    public const string DepthDb = "depth_db";
}

/// <summary>
/// Reads an <see cref="AudioMixerAsset"/> document. Only the version rule throws; content problems
/// (a bad entry, an unknown effect) are skipped or defaulted with a warning naming the asset.
/// </summary>
public static class AudioMixerAssetJsonSerializer
{
    public static void Load(AudioMixerAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        int version = node[AudioMixerAssetKeys.Version]?.GetInt32() ?? AudioMixerAsset.CurrentVersion;
        if (!CanMigrate(version))
        {
            throw new InvalidOperationException(version > AudioMixerAsset.CurrentVersion
                ? $"Audio mixer asset version {version} is newer than supported version {AudioMixerAsset.CurrentVersion}."
                : $"Audio mixer asset version {version} is invalid (the first version is 1).");
        }

        MigrateToCurrent(node);

        asset.Version = AudioMixerAsset.CurrentVersion;
        asset.Buses.Clear();

        if (node[AudioMixerAssetKeys.Buses] is not JArray busesNode)
        {
            return;
        }

        for (int busIndex = 0; busIndex < busesNode.Count; busIndex++)
        {
            if (busesNode[busIndex] is not JObject busNode)
            {
                Warn(asset, $"bus entry {busIndex} is not an object, skipped");
                continue;
            }

            var bus = LoadBus(asset, busNode, busIndex);
            if (bus != null)
            {
                asset.Buses.Add(bus);
            }
        }
    }

    public static bool CanMigrate(int version)
        => version > 0 && version <= AudioMixerAsset.CurrentVersion;

    public static void MigrateToCurrent(JObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node[AudioMixerAssetKeys.Version] = AudioMixerAsset.CurrentVersion;
        node[AudioMixerAssetKeys.SchemaVersion] = AudioMixerAsset.CurrentVersion;
    }

    private static AudioMixerBusData LoadBus(AudioMixerAsset asset, JObject node, int busIndex)
    {
        string name = ReadString(node, AudioMixerAssetKeys.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            Warn(asset, $"bus entry {busIndex} has no name, skipped");
            return null;
        }

        var bus = new AudioMixerBusData { Name = name };
        string parent = ReadString(node, AudioMixerAssetKeys.Parent);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            bus.Parent = parent;
        }

        bus.Volume = ReadNumber(asset, node, AudioMixerAssetKeys.Volume, bus.Volume, name);

        if (node[AudioMixerAssetKeys.Effects] is JArray effectsNode)
        {
            for (int effectIndex = 0; effectIndex < effectsNode.Count; effectIndex++)
            {
                if (effectsNode[effectIndex] is not JObject effectNode)
                {
                    Warn(asset, $"bus '{name}': effect entry {effectIndex} is not an object, skipped");
                    continue;
                }

                var effect = LoadEffect(asset, effectNode, name);
                if (effect != null)
                {
                    bus.Effects.Add(effect);
                }
            }
        }

        if (node[AudioMixerAssetKeys.Sends] is JArray sendsNode)
        {
            for (int sendIndex = 0; sendIndex < sendsNode.Count; sendIndex++)
            {
                if (sendsNode[sendIndex] is not JObject sendNode)
                {
                    Warn(asset, $"bus '{name}': send entry {sendIndex} is not an object, skipped");
                    continue;
                }

                string target = ReadString(sendNode, AudioMixerAssetKeys.Target);
                if (string.IsNullOrWhiteSpace(target))
                {
                    Warn(asset, $"bus '{name}': send entry {sendIndex} has no target, skipped");
                    continue;
                }

                bus.Sends.Add(new AudioMixerSendData(target, ReadNumber(asset, sendNode, AudioMixerAssetKeys.Level, 1f, name)));
            }
        }

        return bus;
    }

    private static AudioMixerEffectData LoadEffect(AudioMixerAsset asset, JObject node, string busName)
    {
        float Number(string key, float defaultValue) => ReadNumber(asset, node, key, defaultValue, busName);

        string type = ReadString(node, AudioMixerAssetKeys.Type);
        switch (type)
        {
            case AudioMixerAssetKeys.BiquadType:
                return new AudioMixerBiquadEffectData(
                    ReadFilterType(asset, node, busName),
                    Number(AudioMixerAssetKeys.FrequencyHz, AudioMixerEffectDefaults.BiquadFrequencyHz),
                    Number(AudioMixerAssetKeys.Q, AudioMixerEffectDefaults.BiquadQ),
                    Number(AudioMixerAssetKeys.GainDb, AudioMixerEffectDefaults.BiquadGainDb));

            case AudioMixerAssetKeys.CompressorType:
                return new AudioMixerCompressorEffectData(
                    Number(AudioMixerAssetKeys.ThresholdDb, AudioMixerEffectDefaults.CompressorThresholdDb),
                    Number(AudioMixerAssetKeys.Ratio, AudioMixerEffectDefaults.CompressorRatio),
                    Number(AudioMixerAssetKeys.KneeDb, AudioMixerEffectDefaults.CompressorKneeDb),
                    Number(AudioMixerAssetKeys.AttackSeconds, AudioMixerEffectDefaults.CompressorAttackSeconds),
                    Number(AudioMixerAssetKeys.ReleaseSeconds, AudioMixerEffectDefaults.CompressorReleaseSeconds),
                    Number(AudioMixerAssetKeys.MakeupGainDb, AudioMixerEffectDefaults.CompressorMakeupGainDb));

            case AudioMixerAssetKeys.ReverbType:
                return new AudioMixerReverbEffectData(
                    Number(AudioMixerAssetKeys.RoomSize, AudioMixerEffectDefaults.ReverbRoomSize),
                    Number(AudioMixerAssetKeys.Damping, AudioMixerEffectDefaults.ReverbDamping),
                    Number(AudioMixerAssetKeys.Wet, AudioMixerEffectDefaults.ReverbWet),
                    Number(AudioMixerAssetKeys.Dry, AudioMixerEffectDefaults.ReverbDry),
                    Number(AudioMixerAssetKeys.StereoSeparation, AudioMixerEffectDefaults.ReverbStereoSeparation));

            case AudioMixerAssetKeys.DuckingType:
                string source = ReadString(node, AudioMixerAssetKeys.Source);
                if (string.IsNullOrWhiteSpace(source))
                {
                    Warn(asset, $"bus '{busName}': ducking effect has no source, skipped");
                    return null;
                }

                return new AudioMixerDuckingEffectData(
                    source,
                    Number(AudioMixerAssetKeys.DepthDb, AudioMixerEffectDefaults.DuckingDepthDb),
                    Number(AudioMixerAssetKeys.ThresholdDb, AudioMixerEffectDefaults.DuckingThresholdDb),
                    Number(AudioMixerAssetKeys.AttackSeconds, AudioMixerEffectDefaults.DuckingAttackSeconds),
                    Number(AudioMixerAssetKeys.ReleaseSeconds, AudioMixerEffectDefaults.DuckingReleaseSeconds));

            default:
                Warn(asset, $"bus '{busName}': unknown effect type '{type}', skipped");
                return null;
        }
    }

    private static BiquadFilterType ReadFilterType(AudioMixerAsset asset, JObject node, string busName)
    {
        var token = node[AudioMixerAssetKeys.Filter];
        if (token == null || token.Type == JTokenType.Null)
        {
            return AudioMixerEffectDefaults.BiquadFilterType;
        }

        string text = token.Type == JTokenType.String ? token.Value<string>() : null;
        if (text != null
            && !int.TryParse(text, out _)
            && Enum.TryParse(text, ignoreCase: true, out BiquadFilterType filterType)
            && Enum.IsDefined(filterType))
        {
            return filterType;
        }

        Warn(asset, $"bus '{busName}': unknown filter '{token}', using {AudioMixerEffectDefaults.BiquadFilterType}");
        return AudioMixerEffectDefaults.BiquadFilterType;
    }

    private static string ReadString(JObject node, string key)
    {
        var token = node[key];
        return token != null && token.Type == JTokenType.String ? token.Value<string>() : null;
    }

    private static float ReadNumber(AudioMixerAsset asset, JObject node, string key, float defaultValue, string busName)
    {
        var token = node[key];
        if (token == null || token.Type == JTokenType.Null)
        {
            return defaultValue;
        }

        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
        {
            return token.Value<float>();
        }

        Warn(asset, $"bus '{busName}': '{key}' is not a number, keeping {defaultValue}");
        return defaultValue;
    }

    private static void Warn(AudioMixerAsset asset, string message)
        => Logs.WriteWarning($"[AudioMixerAsset] '{asset.Name}': {message}");
}
