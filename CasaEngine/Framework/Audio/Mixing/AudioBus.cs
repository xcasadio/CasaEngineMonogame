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
    private readonly List<AudioBusSend> _sends = new();

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

    /// <summary>Largest number of sends a bus holds.</summary>
    public const int MaxSends = 4;

    /// <summary>
    /// Sends of this bus to return buses. They only run on a backend with <see cref="IAudioBusBackend"/> (the software
    /// backend); see <see cref="SetSend"/>.
    /// </summary>
    public IReadOnlyList<AudioBusSend> Sends => _sends;

    /// <summary>Level of the send of this bus to <paramref name="target"/>, or 0 when there is none.</summary>
    public float GetSend(AudioBus target)
    {
        for (var i = 0; i < _sends.Count; i++)
        {
            if (ReferenceEquals(_sends[i].Target, target))
            {
                return _sends[i].Level;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Sends part of this bus to the return bus <paramref name="target"/>: on the audio thread, after the insert effects and
    /// the gain of this bus, its signal times <paramref name="level"/> is added to the buffer of the target, which is mixed
    /// after the buses that feed it (typically a bus with a <see cref="ReverbEffect"/>, a child of Master). The signal still
    /// goes to the parent of this bus as well. A level of 0 removes the send; a value out of [0, 1] is clamped and NaN is
    /// ignored. The send follows the gain of this bus, not the gain of its ancestors. Game thread only.
    /// </summary>
    /// <exception cref="ArgumentNullException">The target is null.</exception>
    /// <exception cref="ArgumentException">The target belongs to another mixer.</exception>
    /// <exception cref="InvalidOperationException">
    /// The send would make a cycle (this bus sending to itself, to a bus that is below it or to a bus that reaches it through
    /// other sends), or the bus already holds <see cref="MaxSends"/> sends.
    /// </exception>
    public void SetSend(AudioBus target, float level)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!ReferenceEquals(target._mixer, _mixer))
        {
            throw new ArgumentException("The target belongs to another audio mixer.", nameof(target));
        }

        if (float.IsNaN(level))
        {
            return;
        }

        var clamped = Math.Clamp(level, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
        var existing = -1;

        for (var i = 0; i < _sends.Count; i++)
        {
            if (ReferenceEquals(_sends[i].Target, target))
            {
                existing = i;
                break;
            }
        }

        if (existing < 0)
        {
            if (clamped <= 0f)
            {
                return;
            }

            if (AudioMixer.Reaches(target, this))
            {
                throw new InvalidOperationException($"A send from the audio bus '{Name}' to '{target.Name}' would make a cycle.");
            }

            if (_sends.Count >= MaxSends)
            {
                throw new InvalidOperationException($"The audio bus '{Name}' already holds {MaxSends} sends.");
            }

            _sends.Add(new AudioBusSend(target, clamped));
        }
        else if (clamped <= 0f)
        {
            _sends.RemoveAt(existing);
        }
        else if (_sends[existing].Level.Equals(clamped))
        {
            return;
        }
        else
        {
            _sends[existing] = new AudioBusSend(target, clamped);
        }

        _mixer.InvalidateSends();
    }

    public override string ToString() => $"{Name} (volume:{_volume} muted:{_isMuted} gain:{EffectiveGain})";
}
