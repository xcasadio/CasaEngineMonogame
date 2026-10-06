using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Audio;

/// <summary>Writes an <see cref="AudioMixerAsset"/> as a <c>.audioMixer</c> document, every field included.</summary>
internal static class EditorAudioMixerAssetJsonWriter
{
    public static void Save(AudioMixerAsset asset, JObject node)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(node);

        EditorJsonSaveHelper.SaveObjectBase(asset, node);
        node[AudioMixerAssetKeys.Type] = nameof(AudioMixerAsset);
        node[AudioMixerAssetKeys.Version] = asset.Version;
        node[AudioMixerAssetKeys.SchemaVersion] = AudioMixerAsset.CurrentVersion;

        var busesNode = new JArray();
        for (int busIndex = 0; busIndex < asset.Buses.Count; busIndex++)
        {
            busesNode.Add(SaveBus(asset.Buses[busIndex]));
        }

        node[AudioMixerAssetKeys.Buses] = busesNode;
    }

    private static JObject SaveBus(AudioMixerBusData bus)
    {
        var effectsNode = new JArray();
        for (int effectIndex = 0; effectIndex < bus.Effects.Count; effectIndex++)
        {
            effectsNode.Add(SaveEffect(bus.Effects[effectIndex]));
        }

        var sendsNode = new JArray();
        for (int sendIndex = 0; sendIndex < bus.Sends.Count; sendIndex++)
        {
            var send = bus.Sends[sendIndex];
            sendsNode.Add(new JObject
            {
                [AudioMixerAssetKeys.Target] = send.Target,
                [AudioMixerAssetKeys.Level] = send.Level,
            });
        }

        return new JObject
        {
            [AudioMixerAssetKeys.Name] = bus.Name,
            [AudioMixerAssetKeys.Parent] = bus.Parent,
            [AudioMixerAssetKeys.Volume] = bus.Volume,
            [AudioMixerAssetKeys.Effects] = effectsNode,
            [AudioMixerAssetKeys.Sends] = sendsNode,
        };
    }

    private static JObject SaveEffect(AudioMixerEffectData effect)
    {
        switch (effect)
        {
            case AudioMixerBiquadEffectData biquad:
                return new JObject
                {
                    [AudioMixerAssetKeys.Type] = AudioMixerAssetKeys.BiquadType,
                    [AudioMixerAssetKeys.Filter] = biquad.FilterType.ToString(),
                    [AudioMixerAssetKeys.FrequencyHz] = biquad.FrequencyHz,
                    [AudioMixerAssetKeys.Q] = biquad.Q,
                    [AudioMixerAssetKeys.GainDb] = biquad.GainDb,
                };

            case AudioMixerCompressorEffectData compressor:
                return new JObject
                {
                    [AudioMixerAssetKeys.Type] = AudioMixerAssetKeys.CompressorType,
                    [AudioMixerAssetKeys.ThresholdDb] = compressor.ThresholdDb,
                    [AudioMixerAssetKeys.Ratio] = compressor.Ratio,
                    [AudioMixerAssetKeys.KneeDb] = compressor.KneeDb,
                    [AudioMixerAssetKeys.AttackSeconds] = compressor.AttackSeconds,
                    [AudioMixerAssetKeys.ReleaseSeconds] = compressor.ReleaseSeconds,
                    [AudioMixerAssetKeys.MakeupGainDb] = compressor.MakeupGainDb,
                };

            case AudioMixerReverbEffectData reverb:
                return new JObject
                {
                    [AudioMixerAssetKeys.Type] = AudioMixerAssetKeys.ReverbType,
                    [AudioMixerAssetKeys.RoomSize] = reverb.RoomSize,
                    [AudioMixerAssetKeys.Damping] = reverb.Damping,
                    [AudioMixerAssetKeys.Wet] = reverb.Wet,
                    [AudioMixerAssetKeys.Dry] = reverb.Dry,
                    [AudioMixerAssetKeys.StereoSeparation] = reverb.StereoSeparation,
                };

            case AudioMixerDuckingEffectData ducking:
                return new JObject
                {
                    [AudioMixerAssetKeys.Type] = AudioMixerAssetKeys.DuckingType,
                    [AudioMixerAssetKeys.Source] = ducking.Source,
                    [AudioMixerAssetKeys.DepthDb] = ducking.DepthDb,
                    [AudioMixerAssetKeys.ThresholdDb] = ducking.ThresholdDb,
                    [AudioMixerAssetKeys.AttackSeconds] = ducking.AttackSeconds,
                    [AudioMixerAssetKeys.ReleaseSeconds] = ducking.ReleaseSeconds,
                };

            default:
                throw new InvalidOperationException($"Unknown audio mixer effect data type '{effect?.GetType().Name}'.");
        }
    }
}
