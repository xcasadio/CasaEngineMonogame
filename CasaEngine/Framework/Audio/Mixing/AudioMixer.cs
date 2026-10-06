using CasaEngine.Framework.Audio.Effects;

namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// Tree of named mixing buses and their effective gains.
/// </summary>
/// <remarks>
/// Gains are recomputed only when a volume or a mute changes, never per frame. Buses are stored
/// in creation order and a parent must exist before its children, so that order is already a
/// valid topological order: a single forward pass recomputes the whole tree.
/// <see cref="Version"/> lets the voice pool detect that it must reapply gains to live voices.
/// </remarks>
public sealed class AudioMixer
{
    private readonly List<AudioBus> _buses = new();
    private readonly Dictionary<string, AudioBus> _busesByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Incremented every time an effective gain may have changed.</summary>
    public int Version { get; private set; }

    /// <summary>Incremented every time an effect is added to or removed from a bus.</summary>
    public int EffectsVersion { get; private set; }

    /// <summary>Incremented every time a send of a bus is added, changed or removed.</summary>
    public int SendsVersion { get; private set; }

    public IReadOnlyList<AudioBus> Buses => _buses;

    /// <summary>The bus every other bus hangs from, or null while the mixer is empty.</summary>
    public AudioBus Root { get; private set; }

    /// <summary>
    /// Creates a bus. <paramref name="parentName"/> must be null for the root bus only, and must
    /// reference an existing bus otherwise.
    /// </summary>
    /// <exception cref="ArgumentException">The name is already used, or the parent is unknown.</exception>
    /// <exception cref="InvalidOperationException">A second root bus is requested.</exception>
    public AudioBus CreateBus(string name, string parentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_busesByName.ContainsKey(name))
        {
            throw new ArgumentException($"An audio bus named '{name}' already exists.", nameof(name));
        }

        AudioBus parent = null;

        if (parentName == null)
        {
            if (Root != null)
            {
                throw new InvalidOperationException(
                    $"The audio mixer already has a root bus ('{Root.Name}'); '{name}' must declare a parent.");
            }
        }
        else
        {
            if (!_busesByName.TryGetValue(parentName, out parent))
            {
                throw new ArgumentException(
                    $"Unknown parent audio bus '{parentName}' for bus '{name}'.", nameof(parentName));
            }
        }

        var bus = new AudioBus(this, name, parent);
        _buses.Add(bus);
        _busesByName.Add(name, bus);

        if (parent == null)
        {
            Root = bus;
        }

        InvalidateGains();
        return bus;
    }

    public bool TryGetBus(string name, out AudioBus bus)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            bus = null;
            return false;
        }

        return _busesByName.TryGetValue(name, out bus);
    }

    /// <exception cref="ArgumentException">No bus carries that name.</exception>
    public AudioBus GetBus(string name)
    {
        if (!TryGetBus(name, out var bus))
        {
            throw new ArgumentException($"Unknown audio bus '{name}'.", nameof(name));
        }

        return bus;
    }

    /// <summary>
    /// Effective gain of a bus by name, or the root gain when the name is unknown or empty.
    /// Used on the playback path, where an asset referencing a removed bus must still be audible
    /// rather than crash.
    /// </summary>
    public float GetEffectiveGain(string busName)
    {
        if (TryGetBus(busName, out var bus))
        {
            return bus.EffectiveGain;
        }

        return Root?.EffectiveGain ?? 1f;
    }

    internal void InvalidateEffects()
    {
        EffectsVersion++;
    }

    internal void InvalidateSends()
    {
        SendsVersion++;
    }

    /// <summary>
    /// True when the signal of <paramref name="from"/> reaches <paramref name="goal"/> (itself included) through parents, sends
    /// and ducking relations: a send from <paramref name="goal"/> to <paramref name="from"/> would then make a cycle.
    /// </summary>
    internal static bool Reaches(AudioBus from, AudioBus goal)
    {
        return Reaches(from, goal, 0);
    }

    private static bool Reaches(AudioBus bus, AudioBus goal, int depth)
    {
        if (ReferenceEquals(bus, goal))
        {
            return true;
        }

        // The graph is acyclic by construction (parents are fixed, sends are checked), so the depth is bounded by the bus count.
        if (depth > 1024)
        {
            return false;
        }

        if (bus.Parent != null && Reaches(bus.Parent, goal, depth + 1))
        {
            return true;
        }

        var sends = bus.Sends;

        for (var i = 0; i < sends.Count; i++)
        {
            if (Reaches(sends[i].Target, goal, depth + 1))
            {
                return true;
            }
        }

        // A ducking relation is an edge from its source to the bus it is inserted on.
        var buses = bus.Mixer._buses;

        for (var b = 0; b < buses.Count; b++)
        {
            var effects = buses[b].Effects;

            for (var e = 0; e < effects.Count; e++)
            {
                if (effects[e] is DuckingEffect ducking && ReferenceEquals(ducking.Source, bus) && Reaches(buses[b], goal, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Recomputes every effective gain and bumps <see cref="Version"/>.</summary>
    internal void InvalidateGains()
    {
        for (var i = 0; i < _buses.Count; i++)
        {
            var bus = _buses[i];

            if (bus.IsMuted)
            {
                bus.EffectiveGain = 0f;
                continue;
            }

            // The parent was created first, so its gain is already up to date.
            bus.EffectiveGain = bus.Parent == null
                ? bus.Volume
                : bus.Parent.EffectiveGain * bus.Volume;
        }

        Version++;
    }
}
