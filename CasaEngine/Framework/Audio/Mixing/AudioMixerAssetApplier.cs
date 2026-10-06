using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio.Effects;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// Applies an <see cref="AudioMixerAsset"/> to the live mixer of an <see cref="AudioService"/> and takes it back (plan
/// decision P46). Idempotent and owned-state: it remembers what it changed, so applying the same asset twice changes
/// nothing, applying another asset moves from one to the other, and <see cref="Release"/> restores what it touched.
/// </summary>
/// <remarks>
/// It never removes, renames or reparents a bus (the engine cannot), and never touches Master, Editor, mutes, the Master
/// limiter, snapshots, nor the effects and sends the game added itself. Game thread only; nothing is logged per frame.
/// </remarks>
public sealed class AudioMixerAssetApplier
{
    private sealed class OwnedEffect
    {
        public AudioMixerEffectData Data;
        public AudioEffect Instance;
    }

    private sealed class OwnedBus
    {
        public string Name;
        public bool HasOriginalVolume;
        public float OriginalVolume;
        public readonly List<OwnedEffect> Effects = new();
        public readonly List<string> SendTargets = new();
    }

    private readonly Dictionary<string, OwnedBus> _owned = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _plannedBuses = new(StringComparer.OrdinalIgnoreCase);
    private bool _warnedNoBusBackend;

    public AudioMixerAssetApplier(AudioService service)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public AudioService Service { get; }

    /// <summary>Number of buses the backend holds, or <see cref="int.MaxValue"/> when it has no bus support.</summary>
    public int BusCapacity => (Service.Backend as IAudioBusBackend)?.BusCapacity ?? int.MaxValue;

    /// <summary>True from the first <see cref="Apply"/> until <see cref="Release"/>.</summary>
    public bool IsApplied { get; private set; }

    /// <summary>Problems of the last <see cref="Apply"/>.</summary>
    public IReadOnlyList<string> LastProblems { get; private set; } = Array.Empty<string>();

    private AudioMixer Mixer => Service.Mixer;

    /// <summary>
    /// Applies the asset in two passes: every bus is created and its volume set first (so a send or a ducking may name a
    /// bus declared later), then the effects and the sends. A failure on one item is logged and the rest goes on.
    /// </summary>
    public void Apply(AudioMixerAsset asset, bool logProblems = true)
    {
        if (asset == null)
        {
            Release();
            return;
        }

        var plan = AudioMixerAssetValidator.Validate(asset, Mixer, BusCapacity);
        LastProblems = plan.Problems;

        if (logProblems)
        {
            for (var i = 0; i < plan.Problems.Count; i++)
            {
                Logs.WriteWarning(plan.Problems[i]);
            }
        }

        _plannedBuses.Clear();
        for (var i = 0; i < plan.Buses.Count; i++)
        {
            _plannedBuses.Add(plan.Buses[i].Name);
        }

        // Buses of a previous asset that this one does not mention give back what they were given (frees slots first).
        var abandoned = new List<OwnedBus>();
        foreach (var owned in _owned.Values)
        {
            if (!_plannedBuses.Contains(owned.Name))
            {
                abandoned.Add(owned);
            }
        }

        for (var i = 0; i < abandoned.Count; i++)
        {
            Retire(abandoned[i]);
            _owned.Remove(abandoned[i].Name);
        }

        IsApplied = true;

        if (!_warnedNoBusBackend && Service.Backend is not IAudioBusBackend && HasEffects(plan))
        {
            _warnedNoBusBackend = true;
            Logs.WriteWarning($"[AudioMixerAsset] '{plan.AssetName}': the audio backend has no bus support, the effects of the mixer asset will not be heard");
        }

        // Pass 1: buses and volumes.
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < plan.Buses.Count; i++)
        {
            var planBus = plan.Buses[i];

            if (!Mixer.TryGetBus(planBus.Name, out var bus))
            {
                try
                {
                    bus = Mixer.CreateBus(planBus.Name, planBus.Parent);
                }
                catch (Exception exception)
                {
                    failed.Add(planBus.Name);
                    Logs.WriteWarning($"[AudioMixerAsset] '{plan.AssetName}': bus '{planBus.Name}' could not be created: {exception.Message}");
                    continue;
                }

                // A created bus stays after Release; its original volume is the one it was created with.
                var created = GetOrAddOwned(planBus.Name);
                created.HasOriginalVolume = true;
                created.OriginalVolume = bus.Volume;
            }

            SetVolume(planBus.Name, bus, planBus.Volume);
        }

        // Pass 2: effects, then sends, in plan order.
        for (var i = 0; i < plan.Buses.Count; i++)
        {
            var planBus = plan.Buses[i];

            if (failed.Contains(planBus.Name) || !Mixer.TryGetBus(planBus.Name, out var bus))
            {
                continue;
            }

            ApplyEffects(plan.AssetName, planBus, bus);
            ApplySends(plan.AssetName, planBus, bus);
        }
    }

    /// <summary>
    /// Fast path for a fader: sets the volume of a bus of the applied asset without validating or applying anything else.
    /// Returns false when nothing is applied, the bus is not one of the applied asset or it does not exist.
    /// </summary>
    public bool TryApplyBusVolume(string busName, float volume)
    {
        if (!IsApplied || string.IsNullOrWhiteSpace(busName) || !_plannedBuses.Contains(busName) || !Mixer.TryGetBus(busName, out var bus))
        {
            return false;
        }

        SetVolume(bus.Name, bus, volume);
        return true;
    }

    /// <summary>Removes the effects and sends this applier added, restores the volumes it changed and forgets everything.</summary>
    public void Release()
    {
        foreach (var owned in _owned.Values)
        {
            Retire(owned);
        }

        _owned.Clear();
        _plannedBuses.Clear();
        LastProblems = Array.Empty<string>();
        IsApplied = false;
    }

    private static bool HasEffects(AudioMixerPlan plan)
    {
        for (var i = 0; i < plan.Buses.Count; i++)
        {
            if (plan.Buses[i].Effects.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private OwnedBus GetOrAddOwned(string name)
    {
        if (!_owned.TryGetValue(name, out var owned))
        {
            owned = new OwnedBus { Name = name };
            _owned.Add(name, owned);
        }

        return owned;
    }

    private void SetVolume(string name, AudioBus bus, float volume)
    {
        var clamped = Math.Clamp(volume, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);

        if (float.IsNaN(volume) || bus.Volume.Equals(clamped))
        {
            return;
        }

        var owned = GetOrAddOwned(name);

        if (!owned.HasOriginalVolume)
        {
            owned.HasOriginalVolume = true;
            owned.OriginalVolume = bus.Volume;
        }

        bus.Volume = clamped;
    }

    // Gives back the volume, the effects and the sends of an owned bus.
    private void Retire(OwnedBus owned)
    {
        Mixer.TryGetBus(owned.Name, out var bus);

        if (bus == null)
        {
            owned.Effects.Clear();
            owned.SendTargets.Clear();
            return;
        }

        for (var i = owned.Effects.Count - 1; i >= 0; i--)
        {
            bus.RemoveEffect(owned.Effects[i].Instance);
        }

        owned.Effects.Clear();

        for (var i = 0; i < owned.SendTargets.Count; i++)
        {
            try
            {
                if (Mixer.TryGetBus(owned.SendTargets[i], out var target))
                {
                    bus.SetSend(target, 0f);
                }
            }
            catch (Exception exception)
            {
                Logs.WriteWarning($"[AudioMixerAsset] bus '{owned.Name}': a send to '{owned.SendTargets[i]}' could not be removed: {exception.Message}");
            }
        }

        owned.SendTargets.Clear();

        if (owned.HasOriginalVolume)
        {
            bus.Volume = owned.OriginalVolume;
            owned.HasOriginalVolume = false;
        }
    }

    private void ApplyEffects(string assetName, AudioMixerPlanBus planBus, AudioBus bus)
    {
        _owned.TryGetValue(planBus.Name, out var owned);
        var planned = planBus.Effects;

        if (owned == null && planned.Count == 0)
        {
            return;
        }

        owned ??= GetOrAddOwned(planBus.Name);
        var index = 0;

        // The common prefix is edited in place; the first mismatch replaces everything from there on.
        while (index < owned.Effects.Count && index < planned.Count)
        {
            var ownedEffect = owned.Effects[index];

            if (!ReferenceEquals(ownedEffect.Instance.Bus, bus) || !TrySync(ownedEffect.Instance, planned[index]))
            {
                break;
            }

            ownedEffect.Data = planned[index];
            index++;
        }

        for (var i = owned.Effects.Count - 1; i >= index; i--)
        {
            bus.RemoveEffect(owned.Effects[i].Instance);
            owned.Effects.RemoveAt(i);
        }

        for (var i = index; i < planned.Count; i++)
        {
            try
            {
                var instance = CreateEffect(planned[i]);
                bus.AddEffect(instance);
                owned.Effects.Add(new OwnedEffect { Data = planned[i], Instance = instance });
            }
            catch (Exception exception)
            {
                Logs.WriteWarning($"[AudioMixerAsset] '{assetName}': bus '{planBus.Name}': an effect could not be added: {exception.Message}");
            }
        }
    }

    private void ApplySends(string assetName, AudioMixerPlanBus planBus, AudioBus bus)
    {
        _owned.TryGetValue(planBus.Name, out var owned);

        if (owned == null && planBus.Sends.Count == 0)
        {
            return;
        }

        owned ??= GetOrAddOwned(planBus.Name);

        for (var i = owned.SendTargets.Count - 1; i >= 0; i--)
        {
            var target = owned.SendTargets[i];
            var stillPlanned = false;

            for (var s = 0; s < planBus.Sends.Count; s++)
            {
                if (string.Equals(planBus.Sends[s].Target, target, StringComparison.OrdinalIgnoreCase))
                {
                    stillPlanned = true;
                    break;
                }
            }

            if (stillPlanned)
            {
                continue;
            }

            try
            {
                if (Mixer.TryGetBus(target, out var targetBus))
                {
                    bus.SetSend(targetBus, 0f);
                }
            }
            catch (Exception exception)
            {
                Logs.WriteWarning($"[AudioMixerAsset] '{assetName}': bus '{planBus.Name}': the send to '{target}' could not be removed: {exception.Message}");
            }

            owned.SendTargets.RemoveAt(i);
        }

        for (var s = 0; s < planBus.Sends.Count; s++)
        {
            var send = planBus.Sends[s];

            try
            {
                bus.SetSend(Mixer.GetBus(send.Target), send.Level);

                if (!ContainsIgnoreCase(owned.SendTargets, send.Target))
                {
                    owned.SendTargets.Add(send.Target);
                }
            }
            catch (Exception exception)
            {
                Logs.WriteWarning($"[AudioMixerAsset] '{assetName}': bus '{planBus.Name}': the send to '{send.Target}' could not be set: {exception.Message}");
            }
        }
    }

    private static bool ContainsIgnoreCase(List<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private AudioEffect CreateEffect(AudioMixerEffectData data)
    {
        switch (data)
        {
            case AudioMixerBiquadEffectData biquad:
                return new BiquadFilterEffect(biquad.FilterType, biquad.FrequencyHz, biquad.Q, biquad.GainDb);
            case AudioMixerCompressorEffectData compressor:
                return new CompressorEffect(compressor.ThresholdDb, compressor.Ratio, compressor.KneeDb, compressor.AttackSeconds, compressor.ReleaseSeconds, compressor.MakeupGainDb);
            case AudioMixerReverbEffectData reverb:
                return new ReverbEffect(reverb.RoomSize, reverb.Damping, reverb.Wet, reverb.Dry, reverb.StereoSeparation);
            case AudioMixerDuckingEffectData ducking:
                return new DuckingEffect(Mixer.GetBus(ducking.Source), ducking.DepthDb, ducking.ThresholdDb, ducking.AttackSeconds, ducking.ReleaseSeconds);
            default:
                throw new ArgumentException($"Unknown mixer effect data {data?.GetType().Name ?? "null"}.", nameof(data));
        }
    }

    // Brings a live effect to the values of data when it is of the same kind (same source for a ducking); false otherwise.
    // The target values come from a fresh effect so the engine clamps them, and only the differing ones are written.
    private bool TrySync(AudioEffect live, AudioMixerEffectData data)
    {
        AudioEffect probe;

        try
        {
            probe = CreateEffect(data);
        }
        catch (Exception)
        {
            return false;
        }

        switch (live)
        {
            case BiquadFilterEffect biquad when probe is BiquadFilterEffect wanted:
                if (biquad.Type != wanted.Type) { biquad.Type = wanted.Type; }
                if (!biquad.FrequencyHz.Equals(wanted.FrequencyHz)) { biquad.FrequencyHz = wanted.FrequencyHz; }
                if (!biquad.Q.Equals(wanted.Q)) { biquad.Q = wanted.Q; }
                if (!biquad.GainDb.Equals(wanted.GainDb)) { biquad.GainDb = wanted.GainDb; }
                return true;
            case CompressorEffect compressor when probe is CompressorEffect wanted:
                if (!compressor.ThresholdDb.Equals(wanted.ThresholdDb)) { compressor.ThresholdDb = wanted.ThresholdDb; }
                if (!compressor.Ratio.Equals(wanted.Ratio)) { compressor.Ratio = wanted.Ratio; }
                if (!compressor.KneeDb.Equals(wanted.KneeDb)) { compressor.KneeDb = wanted.KneeDb; }
                if (!compressor.AttackSeconds.Equals(wanted.AttackSeconds)) { compressor.AttackSeconds = wanted.AttackSeconds; }
                if (!compressor.ReleaseSeconds.Equals(wanted.ReleaseSeconds)) { compressor.ReleaseSeconds = wanted.ReleaseSeconds; }
                if (!compressor.MakeupGainDb.Equals(wanted.MakeupGainDb)) { compressor.MakeupGainDb = wanted.MakeupGainDb; }
                return true;
            case ReverbEffect reverb when probe is ReverbEffect wanted:
                if (!reverb.RoomSize.Equals(wanted.RoomSize)) { reverb.RoomSize = wanted.RoomSize; }
                if (!reverb.Damping.Equals(wanted.Damping)) { reverb.Damping = wanted.Damping; }
                if (!reverb.Wet.Equals(wanted.Wet)) { reverb.Wet = wanted.Wet; }
                if (!reverb.Dry.Equals(wanted.Dry)) { reverb.Dry = wanted.Dry; }
                if (!reverb.StereoSeparation.Equals(wanted.StereoSeparation)) { reverb.StereoSeparation = wanted.StereoSeparation; }
                return true;
            case DuckingEffect ducking when probe is DuckingEffect wanted:
                // The source is fixed at construction: another source is a mismatch, the instance is replaced.
                if (!ReferenceEquals(ducking.Source, wanted.Source))
                {
                    return false;
                }

                if (!ducking.DepthDb.Equals(wanted.DepthDb)) { ducking.DepthDb = wanted.DepthDb; }
                if (!ducking.ThresholdDb.Equals(wanted.ThresholdDb)) { ducking.ThresholdDb = wanted.ThresholdDb; }
                if (!ducking.AttackSeconds.Equals(wanted.AttackSeconds)) { ducking.AttackSeconds = wanted.AttackSeconds; }
                if (!ducking.ReleaseSeconds.Equals(wanted.ReleaseSeconds)) { ducking.ReleaseSeconds = wanted.ReleaseSeconds; }
                return true;
            default:
                return false;
        }
    }
}
