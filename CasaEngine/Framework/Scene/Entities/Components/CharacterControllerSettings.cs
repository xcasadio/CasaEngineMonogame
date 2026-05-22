using BulletSharp;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public sealed class CharacterControllerSettings
{
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
        MaxSlopeAngle = other.MaxSlopeAngle;
        GroundSnapDistance = other.GroundSnapDistance;
        CollisionGroup = other.CollisionGroup;
        CollisionMask = other.CollisionMask;
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

    public float MaxSlopeAngle { get; set; } = 45f;

    public float GroundSnapDistance { get; set; } = 0.15f;

    public CollisionFilterGroups CollisionGroup { get; set; } = CollisionFilterGroups.DefaultFilter;

    public CollisionFilterGroups CollisionMask { get; set; } = CollisionFilterGroups.AllFilter;

    public bool HitTriggers { get; set; }

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
        MaxSlopeAngle = ReadSingle(element, "max_slope_angle", MaxSlopeAngle);
        GroundSnapDistance = ReadSingle(element, "ground_snap_distance", GroundSnapDistance);
        CollisionGroup = ReadEnum(element, "collision_group", CollisionGroup);
        CollisionMask = ReadEnum(element, "collision_mask", CollisionMask);
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

        if (MaxSlopeAngle < 0f || MaxSlopeAngle >= 90f)
        {
            throw new InvalidOperationException("Character controller max slope angle must be in [0, 90).");
        }

        if (GroundSnapDistance < 0f)
        {
            throw new InvalidOperationException("Character controller ground snap distance cannot be negative.");
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

    private static T ReadEnum<T>(JObject element, string propertyName, T defaultValue) where T : struct
    {
        var token = element[propertyName];
        return token == null ? defaultValue : Enum.Parse<T>(token.Value<string>()!, ignoreCase: true);
    }
}