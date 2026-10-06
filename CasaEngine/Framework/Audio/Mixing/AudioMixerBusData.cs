namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>A send of a bus to another bus, at a linear level.</summary>
public sealed record AudioMixerSendData(string Target, float Level);

/// <summary>A bus declared by an <see cref="AudioMixerAsset"/>.</summary>
public sealed class AudioMixerBusData
{
    private float _volume = 1f;

    public string Name { get; set; } = string.Empty;

    public string Parent { get; set; } = AudioBusNames.Master;

    /// <summary>Volume in [0,1]. Out of range values are clamped and NaN is ignored, like <see cref="AudioBus.Volume"/>.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            if (float.IsNaN(value))
            {
                return;
            }

            _volume = Math.Clamp(value, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
        }
    }

    public List<AudioMixerEffectData> Effects { get; } = new();

    public List<AudioMixerSendData> Sends { get; } = new();
}
