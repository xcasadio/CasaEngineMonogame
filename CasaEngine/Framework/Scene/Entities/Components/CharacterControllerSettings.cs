using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public sealed class CharacterControllerSettings
{
    private string _resolvedProfileName;
    private uint _sweepChannelMask;

    public CharacterControllerSettings()
    {
    }

    public CharacterControllerSettings(CharacterControllerSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Radius = other.Radius;
        Height = other.Height;
        SkinWidth = other.SkinWidth;
        MaxHorizontalSpeed = other.MaxHorizontalSpeed;
        Acceleration = other.Acceleration;
        Deceleration = other.Deceleration;
        Gravity = other.Gravity;
        JumpSpeed = other.JumpSpeed;
        CoyoteTimeSeconds = other.CoyoteTimeSeconds;
        JumpBufferSeconds = other.JumpBufferSeconds;
        DashSpeed = other.DashSpeed;
        DashDurationSeconds = other.DashDurationSeconds;
        DashCooldownSeconds = other.DashCooldownSeconds;
        MaxSlopeAngle = other.MaxSlopeAngle;
        GroundSnapDistance = other.GroundSnapDistance;
        StepHeight = other.StepHeight;
        ProfileName = other.ProfileName;
        HitTriggers = other.HitTriggers;
    }

    public float Radius { get; set; } = 0.35f;

    public float Height { get; set; } = 1.8f;

    public float SkinWidth { get; set; } = 0.03f;

    public float MaxHorizontalSpeed { get; set; } = 5f;

    public float Acceleration { get; set; } = 30f;

    public float Deceleration { get; set; } = 35f;

    public float Gravity { get; set; } = 9.81f;

    public float JumpSpeed { get; set; } = 5f;

    public float CoyoteTimeSeconds { get; set; }

    public float JumpBufferSeconds { get; set; }

    public float DashSpeed { get; set; }

    public float DashDurationSeconds { get; set; }

    public float DashCooldownSeconds { get; set; }

    public float MaxSlopeAngle { get; set; } = 45f;

    public float GroundSnapDistance { get; set; } = 0.15f;

    public float StepHeight { get; set; }

    /// Collision profile of the character. Its blocked channels drive the ground and step sweeps.
    public string ProfileName { get; set; } = CollisionProfileNames.Pawn;

    public bool HitTriggers { get; set; }

    /// <summary>
    /// Channels the character sweeps against: what its own collision profile blocks.
    /// Resolved once per profile name, never looked up again while the name does not change.
    /// </summary>
    public uint GetSweepChannelMask()
    {
        if (!ReferenceEquals(_resolvedProfileName, ProfileName))
        {
            _sweepChannelMask = GameSettings.PhysicsEngineSettings.CollisionProfiles.GetResolved(ProfileName).BlockMask;
            _resolvedProfileName = ProfileName;
        }

        return _sweepChannelMask;
    }

    public CharacterControllerSettings Clone()
    {
        return new CharacterControllerSettings(this);
    }

    public void Load(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        Radius = ReadSingle(element, "radius", Radius);
        Height = ReadSingle(element, "height", Height);
        SkinWidth = ReadSingle(element, "skin_width", SkinWidth);
        MaxHorizontalSpeed = ReadSingle(element, "max_horizontal_speed", MaxHorizontalSpeed);
        Acceleration = ReadSingle(element, "acceleration", Acceleration);
        Deceleration = ReadSingle(element, "deceleration", Deceleration);
        Gravity = ReadSingle(element, "gravity", Gravity);
        JumpSpeed = ReadSingle(element, "jump_speed", JumpSpeed);
        CoyoteTimeSeconds = ReadSingle(element, "coyote_time_seconds", CoyoteTimeSeconds);
        JumpBufferSeconds = ReadSingle(element, "jump_buffer_seconds", JumpBufferSeconds);
        DashSpeed = ReadSingle(element, "dash_speed", DashSpeed);
        DashDurationSeconds = ReadSingle(element, "dash_duration_seconds", DashDurationSeconds);
        DashCooldownSeconds = ReadSingle(element, "dash_cooldown_seconds", DashCooldownSeconds);
        MaxSlopeAngle = ReadSingle(element, "max_slope_angle", MaxSlopeAngle);
        GroundSnapDistance = ReadSingle(element, "ground_snap_distance", GroundSnapDistance);
        StepHeight = ReadSingle(element, "step_height", StepHeight);
        ProfileName = element["collision_profile"]?.Value<string>() ?? ProfileName;
        HitTriggers = ReadBoolean(element, "hit_triggers", HitTriggers);

        Validate();
    }

    public void Validate()
    {
        if (Radius <= 0f)
        {
            throw new InvalidOperationException("Character controller radius must be greater than zero.");
        }

        if (Height < Radius * 2f)
        {
            throw new InvalidOperationException("Character controller height must be at least twice the radius.");
        }

        if (SkinWidth < 0f || SkinWidth >= Radius)
        {
            throw new InvalidOperationException("Character controller skin width must be positive and smaller than the radius.");
        }

        if (MaxHorizontalSpeed < 0f)
        {
            throw new InvalidOperationException("Character controller max horizontal speed cannot be negative.");
        }

        if (Acceleration < 0f || Deceleration < 0f)
        {
            throw new InvalidOperationException("Character controller acceleration values cannot be negative.");
        }

        if (Gravity < 0f || JumpSpeed < 0f)
        {
            throw new InvalidOperationException("Character controller gravity and jump speed cannot be negative.");
        }

        if (CoyoteTimeSeconds < 0f || JumpBufferSeconds < 0f)
        {
            throw new InvalidOperationException("Character controller jump assist timings cannot be negative.");
        }

        if (DashSpeed < 0f || DashDurationSeconds < 0f || DashCooldownSeconds < 0f)
        {
            throw new InvalidOperationException("Character controller dash settings cannot be negative.");
        }

        if (MaxSlopeAngle < 0f || MaxSlopeAngle >= 90f)
        {
            throw new InvalidOperationException("Character controller max slope angle must be in [0, 90).");
        }

        if (GroundSnapDistance < 0f)
        {
            throw new InvalidOperationException("Character controller ground snap distance cannot be negative.");
        }

        if (StepHeight < 0f)
        {
            throw new InvalidOperationException("Character controller step height cannot be negative.");
        }

        if (string.IsNullOrEmpty(ProfileName))
        {
            throw new InvalidOperationException("Character controller collision profile name cannot be empty.");
        }
    }

    private static float ReadSingle(JObject element, string propertyName, float defaultValue)
    {
        return element[propertyName]?.Value<float>() ?? defaultValue;
    }

    private static bool ReadBoolean(JObject element, string propertyName, bool defaultValue)
    {
        return element[propertyName]?.Value<bool>() ?? defaultValue;
    }
}