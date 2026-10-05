using CasaEngine.Framework.Audio.Effects;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// A named mixing bus. This is what the engine calls a "channel": voices are routed to a bus,
/// and the bus applies its volume and mute on top of the per-voice volume.
/// </summary>
/// <remarks>
/// The parent is set at creation and never changes, so the bus graph is a tree by construction
/// and cannot contain a cycle. Buses are created through <see cref="AudioMixer.CreateBus"/>.
/// </remarks>
public sealed class AudioBus
{
    private readonly AudioMixer _mixer;
    private float _volume = 1f;
    private bool _isMuted;
    private readonly List<AudioEffect> _effects = new();

    internal AudioBus(AudioMixer mixer, string name, AudioBus parent)
    {
        _mixer = mixer;
        Name = name;
        Parent = parent;
        EffectiveGain = 1f;
    }

    public string Name { get; }

    /// <summary>Null for the root bus.</summary>
    public AudioBus Parent { get; }

    /// <summary>
    /// Volume of this bus alone, in [0,1]. Out of range values are clamped and NaN is ignored:
    /// a bad value coming from a UI slider must not silence the game.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            if (float.IsNaN(value))
            {
                return;
            }

            var clamped = Math.Clamp(value, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
            if (clamped.Equals(_volume))
            {
                return;
            }

            _volume = clamped;
            _mixer.InvalidateGains();
        }
    }

    /// <summary>Muting a bus mutes every bus below it, without touching their volumes.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (value == _isMuted)
            {
                return;
            }

            _isMuted = value;
            _mixer.InvalidateGains();
        }
    }

    /// <summary>
    /// Volume of the whole chain up to the root: the product of the volumes, or 0 when this bus
    /// or one of its ancestors is muted. Recomputed by the mixer when something changes, never
    /// per frame.
    /// </summary>
    public float EffectiveGain { get; internal set; }

    /// <summary>Largest number of insert effects a bus holds.</summary>
    public const int MaxEffects = 4;

    /// <summary>
    /// Insert effects of this bus, in the order they run (insertion order). They only run on a backend with
    /// <see cref="IAudioBusBackend"/> (the software backend); see <see cref="AudioEffect"/>.
    /// </summary>
    public IReadOnlyList<AudioEffect> Effects => _effects;

    /// <summary>Appends an insert effect after the ones already on this bus. Game thread only.</summary>
    /// <exception cref="ArgumentNullException">The effect is null.</exception>
    /// <exception cref="InvalidOperationException">The effect is already on a bus, or the bus holds <see cref="MaxEffects"/> effects.</exception>
    public void AddEffect(AudioEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (effect.Bus != null)
        {
            throw new InvalidOperationException($"The effect is already inserted on the audio bus '{effect.Bus.Name}'.");
        }

        if (_effects.Count >= MaxEffects)
        {
            throw new InvalidOperationException($"The audio bus '{Name}' already holds {MaxEffects} effects.");
        }

        _effects.Add(effect);
        effect.Bus = this;
        _mixer.InvalidateEffects();
    }

    /// <summary>Removes an insert effect; the ones after it move up. Returns false when it is not on this bus. Game thread only.</summary>
    public bool RemoveEffect(AudioEffect effect)
    {
        if (effect == null || !_effects.Remove(effect))
        {
            return false;
        }

        effect.Bus = null;
        _mixer.InvalidateEffects();
        return true;
    }

    public override string ToString() => $"{Name} (volume:{_volume} muted:{_isMuted} gain:{EffectiveGain})";
}
