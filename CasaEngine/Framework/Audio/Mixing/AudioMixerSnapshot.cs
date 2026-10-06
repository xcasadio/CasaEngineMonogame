using CasaEngine.Framework.Audio.Effects;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// A captured state of the mixer (plan decision P18, task T5.5): the own volume of each bus and the parameters of each insert
/// effect. <see cref="Capture"/> reads them; <see cref="AudioService.ApplySnapshot"/> brings the mixer back to them.
/// </summary>
/// <remarks>
/// <para>
/// What is captured: <see cref="AudioBus.Volume"/> of every bus (Master's included) and, for every insert effect of those buses, the
/// parameter snapshot the effect holds. Effect parameters are immutable objects the effect swaps atomically, so capturing one is
/// taking a reference to it. What is never captured nor applied: the Editor bus (its volume and its effects), and the mute of
/// every bus (<see cref="AudioBus.IsMuted"/>, which the project settings drive for Master), so a snapshot taken before a mute
/// does not undo it. The Master limiter of <see cref="AudioService.MasterLimiter"/> is not an insert effect and is not captured.
/// </para>
/// <para>
/// A snapshot keeps its buses and effects by reference (buses are never removed from a mixer). A bus created after the capture is
/// left alone; an effect removed from its bus since the capture is skipped; an effect added since is left alone. A snapshot only
/// applies to the mixer it was captured from.
/// </para>
/// <para>
/// How it is applied: the bus volumes ramp over the duration given to <see cref="AudioService.ApplySnapshot"/>, with
/// <see cref="AudioService.FadeBus"/> (so on the audio thread, sample by sample, under <see cref="IAudioBusBackend"/>, stepped per
/// frame otherwise). The effect parameters are not ramped: they are published at once when the snapshot is applied, and only with
/// <see cref="IAudioBusBackend"/> since the effects are absent elsewhere.
/// </para>
/// </remarks>
public sealed class AudioMixerSnapshot
{
    private readonly BusEntry[] _buses;
    private readonly EffectEntry[] _effects;

    private AudioMixerSnapshot(AudioMixer mixer, BusEntry[] buses, EffectEntry[] effects)
    {
        Mixer = mixer;
        _buses = buses;
        _effects = effects;
    }

    private readonly record struct BusEntry(AudioBus Bus, float Volume);

    private readonly record struct EffectEntry(AudioEffect Effect, object Parameters);

    /// <summary>The mixer this snapshot was captured from.</summary>
    public AudioMixer Mixer { get; }

    /// <summary>Number of buses whose volume the snapshot holds.</summary>
    public int BusCount => _buses.Length;

    /// <summary>Number of insert effects whose parameters the snapshot holds.</summary>
    public int EffectCount => _effects.Length;

    /// <summary>The volume the snapshot holds for a bus, or false when it holds none (unknown bus, Editor bus, bus created later).</summary>
    public bool TryGetBusVolume(string busName, out float volume)
    {
        for (var i = 0; i < _buses.Length; i++)
        {
            if (string.Equals(_buses[i].Bus.Name, busName, StringComparison.OrdinalIgnoreCase))
            {
                volume = _buses[i].Volume;
                return true;
            }
        }

        volume = 0f;
        return false;
    }

    /// <summary>Captures the volume of every bus and the parameters of every insert effect of <paramref name="mixer"/>, the Editor bus excepted. Allocates; game thread.</summary>
    /// <exception cref="ArgumentNullException">The mixer is null.</exception>
    public static AudioMixerSnapshot Capture(AudioMixer mixer)
    {
        ArgumentNullException.ThrowIfNull(mixer);

        var buses = new List<BusEntry>();
        var effects = new List<EffectEntry>();

        for (var i = 0; i < mixer.Buses.Count; i++)
        {
            var bus = mixer.Buses[i];

            if (IsEditorBus(bus))
            {
                continue;
            }

            buses.Add(new BusEntry(bus, bus.Volume));

            for (var e = 0; e < bus.Effects.Count; e++)
            {
                var effect = bus.Effects[e];
                effects.Add(new EffectEntry(effect, effect.CaptureParameters()));
            }
        }

        return new AudioMixerSnapshot(mixer, buses.ToArray(), effects.ToArray());
    }

    private static bool IsEditorBus(AudioBus bus)
    {
        return string.Equals(bus.Name, AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Publishes the captured parameters of the effects that are still inserted on a bus.</summary>
    internal void RestoreEffectParameters()
    {
        for (var i = 0; i < _effects.Length; i++)
        {
            var entry = _effects[i];

            if (entry.Effect.Bus != null && !IsEditorBus(entry.Effect.Bus))
            {
                entry.Effect.RestoreParameters(entry.Parameters);
            }
        }
    }

    /// <summary>Ramps every captured bus volume back over <paramref name="durationSeconds"/>, through <see cref="AudioService.FadeBus"/>.</summary>
    internal void RestoreBusVolumes(AudioService service, float durationSeconds)
    {
        for (var i = 0; i < _buses.Length; i++)
        {
            service.FadeBus(_buses[i].Bus.Name, _buses[i].Volume, durationSeconds);
        }
    }
}
