using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

/// <summary>
/// Response of a collision profile against one collision channel.
/// </summary>
public enum CollisionResponse
{
    Ignore,
    Overlap,
    Block
}

/// <summary>
/// Bit masks over the project collision channel table.
/// </summary>
public static class ChannelMask
{
    public const uint None = 0u;
    public const uint All = uint.MaxValue;

    public static uint FromChannel(int channel)
    {
        return 1u << channel;
    }
}

/// <summary>
/// Channels reserved by the engine. A project may append its own channels after those.
/// </summary>
public static class CollisionChannels
{
    public const int WorldStatic = 0;
    public const int WorldDynamic = 1;
    public const int Pawn = 2;
    public const int Trigger = 3;
    public const int AttackVolume = 4;
    public const int DamageableVolume = 5;

    public const int ReservedCount = 6;
    public const int MaxCount = 32;
}

/// <summary>
/// Profiles reserved by the engine. A project may append its own profiles after those.
/// </summary>
public static class CollisionProfileIds
{
    public const int WorldStatic = 0;
    public const int WorldDynamic = 1;
    public const int Pawn = 2;
    public const int Trigger = 3;
    public const int AttackVolume = 4;
    public const int DamageableVolume = 5;
}

/// <summary>
/// Names of the profiles reserved by the engine.
/// </summary>
public static class CollisionProfileNames
{
    public const string WorldStatic = "WorldStatic";
    public const string WorldDynamic = "WorldDynamic";
    public const string Pawn = "Pawn";
    public const string Trigger = "Trigger";
    public const string AttackVolume = "AttackVolume";
    public const string DamageableVolume = "DamageableVolume";
}

/// <summary>
/// Project data: what an object is (its channel) and how it answers every other channel.
/// </summary>
public sealed class CollisionProfile
{
    private readonly CollisionResponse[] _responses = new CollisionResponse[CollisionChannels.MaxCount];

    public CollisionProfile(string name, int channel)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A collision profile requires a name.", nameof(name));
        }

        if (channel < 0 || channel >= CollisionChannels.MaxCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Collision channel index is out of range.");
        }

        Name = name;
        Channel = channel;
    }

    public string Name { get; }

    public int Channel { get; }

    public Color? DebugColor { get; set; }

    public CollisionResponse GetResponse(int channel)
    {
        return _responses[channel];
    }

    public CollisionProfile SetResponse(int channel, CollisionResponse response)
    {
        if (channel < 0 || channel >= CollisionChannels.MaxCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Collision channel index is out of range.");
        }

        _responses[channel] = response;
        return this;
    }

    public CollisionProfile SetResponseToAllChannels(CollisionResponse response)
    {
        for (int i = 0; i < _responses.Length; i++)
        {
            _responses[i] = response;
        }

        return this;
    }
}

/// <summary>
/// A collision profile lowered to the bit masks used by the physics backend.
/// Computed once when the profile table is resolved, never recomputed at runtime.
/// </summary>
public readonly struct ResolvedCollisionProfile
{
    public ResolvedCollisionProfile(int id, uint groupBit, uint broadphaseMask, uint blockMask, uint overlapMask)
    {
        Id = id;
        GroupBit = groupBit;
        BroadphaseMask = broadphaseMask;
        BlockMask = blockMask;
        OverlapMask = overlapMask;
    }

    /// <summary>Index of the profile in the table it was resolved from.</summary>
    public int Id { get; }

    /// <summary>Bit of the channel this profile belongs to.</summary>
    public uint GroupBit { get; }

    /// <summary>Channels this profile does not ignore.</summary>
    public uint BroadphaseMask { get; }

    /// <summary>Channels this profile blocks.</summary>
    public uint BlockMask { get; }

    /// <summary>Channels this profile only overlaps.</summary>
    public uint OverlapMask { get; }

    /// <summary>A profile that blocks nothing is a sensor: overlap events, no contact response.</summary>
    public bool IsSensor => BlockMask == 0u;
}

/// <summary>
/// The project channel table and its named profiles. Profiles are referenced by name in assets and
/// resolved once to an index; runtime code only ever passes indices and bit masks.
/// </summary>
public sealed class CollisionProfileTable
{
    private readonly List<string> _channelNames = new();
    private readonly List<CollisionProfile> _profiles = new();
    private readonly Dictionary<string, int> _profileIdsByName = new(StringComparer.Ordinal);
    private ResolvedCollisionProfile[] _resolved = Array.Empty<ResolvedCollisionProfile>();
    private bool _isResolved;

    public int ChannelCount => _channelNames.Count;

    public int ProfileCount => _profiles.Count;

    public int AddChannel(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A collision channel requires a name.", nameof(name));
        }

        if (_channelNames.Count == CollisionChannels.MaxCount)
        {
            throw new InvalidOperationException($"A project cannot define more than {CollisionChannels.MaxCount} collision channels.");
        }

        if (_channelNames.Contains(name))
        {
            throw new InvalidOperationException($"Collision channel '{name}' is already defined.");
        }

        _channelNames.Add(name);
        _isResolved = false;
        return _channelNames.Count - 1;
    }

    public int GetChannel(string name)
    {
        int index = _channelNames.IndexOf(name);
        if (index < 0)
        {
            throw new InvalidOperationException($"Unknown collision channel '{name}'.");
        }

        return index;
    }

    public string GetChannelName(int channel)
    {
        return _channelNames[channel];
    }

    public int AddProfile(CollisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Channel >= _channelNames.Count)
        {
            throw new InvalidOperationException($"Collision profile '{profile.Name}' uses undefined channel {profile.Channel}.");
        }

        if (_profileIdsByName.ContainsKey(profile.Name))
        {
            throw new InvalidOperationException($"Collision profile '{profile.Name}' is already defined.");
        }

        _profiles.Add(profile);
        int id = _profiles.Count - 1;
        _profileIdsByName.Add(profile.Name, id);
        _isResolved = false;
        return id;
    }

    public int GetProfileId(string name)
    {
        if (!_profileIdsByName.TryGetValue(name, out int id))
        {
            throw new InvalidOperationException($"Unknown collision profile '{name}'.");
        }

        return id;
    }

    public bool TryGetProfileId(string name, out int id)
    {
        return _profileIdsByName.TryGetValue(name, out id);
    }

    public CollisionProfile GetProfile(int id)
    {
        return _profiles[id];
    }

    public ResolvedCollisionProfile GetResolved(int id)
    {
        if (!_isResolved)
        {
            Resolve();
        }

        return _resolved[id];
    }

    public ResolvedCollisionProfile GetResolved(string name)
    {
        return GetResolved(GetProfileId(name));
    }

    /// <summary>
    /// Profile of a body described by a <see cref="PhysicsDefinition"/>: its explicit profile name when
    /// it has one, otherwise the reserved profile matching its physics type.
    /// </summary>
    public int ResolveProfileId(PhysicsDefinition physicsDefinition)
    {
        ArgumentNullException.ThrowIfNull(physicsDefinition);

        if (!string.IsNullOrEmpty(physicsDefinition.ProfileName))
        {
            return GetProfileId(physicsDefinition.ProfileName);
        }

        return physicsDefinition.PhysicsType switch
        {
            PhysicsType.Static => CollisionProfileIds.WorldStatic,
            PhysicsType.Kinetic => CollisionProfileIds.Pawn,
            _ => CollisionProfileIds.WorldDynamic
        };
    }

    private void Resolve()
    {
        var resolved = new ResolvedCollisionProfile[_profiles.Count];

        for (int id = 0; id < _profiles.Count; id++)
        {
            var profile = _profiles[id];
            uint broadphaseMask = 0u;
            uint blockMask = 0u;
            uint overlapMask = 0u;

            for (int channel = 0; channel < CollisionChannels.MaxCount; channel++)
            {
                uint bit = 1u << channel;
                switch (profile.GetResponse(channel))
                {
                    case CollisionResponse.Block:
                        broadphaseMask |= bit;
                        blockMask |= bit;
                        break;
                    case CollisionResponse.Overlap:
                        broadphaseMask |= bit;
                        overlapMask |= bit;
                        break;
                }
            }

            resolved[id] = new ResolvedCollisionProfile(id, 1u << profile.Channel, broadphaseMask, blockMask, overlapMask);
        }

        _resolved = resolved;
        _isResolved = true;
    }
}
