using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Physics;

public class PhysicsDefinition
{
    public PhysicsType PhysicsType { get; set; }
    public float AngularDamping { get; set; } = 0f;
    public Vector3 AngularFactor { get; set; } = Vector3.One;
    public float Friction { get; set; } = 0.5f;
    public float LinearDamping { get; set; } = 0f;
    public Vector3 LinearFactor { get; set; } = Vector3.One;

    /// Squared velocity (linear² + angular²) below which a dynamic body is allowed to sleep once it
    /// stayed under it for <see cref="SleepDelaySeconds"/>. The default mirrors the former Bullet
    /// deactivation (0.8 units/s): a slow roller settles instead of creeping forever.
    /// A negative value means the body never sleeps.
    public float SleepThreshold { get; set; } = 0.64f;

    /// Seconds a body must stay under <see cref="SleepThreshold"/> before it may sleep (Bullet used 2 s).
    public const float SleepDelaySeconds = 2f;

    public float Mass { get; set; } = 0f;

    public float Restitution { get; set; } = 0f;
    public bool ApplyGravity { get; set; } = true;
    public Color? DebugColor { get; set; }

    /// Name of the collision profile of this body. When empty the profile is derived from <see cref="PhysicsType"/>.
    public string ProfileName { get; set; }

    public PhysicsDefinition()
    {

    }

    public PhysicsDefinition(PhysicsDefinition other)
    {
        PhysicsType = other.PhysicsType;
        AngularDamping = other.AngularDamping;
        AngularFactor = other.AngularFactor;
        Friction = other.Friction;
        LinearDamping = other.LinearDamping;
        LinearFactor = other.LinearFactor;
        SleepThreshold = other.SleepThreshold;
        Mass = other.Mass;
        Restitution = other.Restitution;
        ApplyGravity = other.ApplyGravity;
        DebugColor = other.DebugColor;
        ProfileName = other.ProfileName;
    }

    public void Load(JObject element)
    {
        //A missing physics_type keeps the definition's current type (Static for a fresh definition), like every other key.
        PhysicsType = element["physics_type"] is { Type: not JTokenType.Null } physicsTypeToken
            ? physicsTypeToken.GetEnum<PhysicsType>()
            : PhysicsType;
        AngularDamping = GetSingleOrDefault(element, "angular_damping", AngularDamping);
        AngularFactor = GetVector3OrDefault(element, "angular_factor", AngularFactor);
        Friction = GetSingleOrDefault(element, "friction", Friction);
        LinearDamping = GetSingleOrDefault(element, "linear_damping", LinearDamping);
        LinearFactor = GetVector3OrDefault(element, "linear_factor", LinearFactor);
        SleepThreshold = GetSingleOrDefault(element, "sleep_threshold", SleepThreshold);
        Mass = GetSingleOrDefault(element, "mass", Mass);
        Restitution = GetSingleOrDefault(element, "restitution", Restitution);
        ApplyGravity = GetBooleanOrDefault(element, "apply_gravity", ApplyGravity);
        ProfileName = element["collision_profile"]?.Value<string>();

        var debugColorElement = element["debug_color"];
        if (debugColorElement is JObject debugColorObject)
        {
            DebugColor = debugColorObject.GetColor();
        }
    }

    private static float GetSingleOrDefault(JObject element, string key, float defaultValue)
    {
        return element[key] is { } token ? token.GetSingle() : defaultValue;
    }

    private static bool GetBooleanOrDefault(JObject element, string key, bool defaultValue)
    {
        return element[key] is { } token ? token.GetBoolean() : defaultValue;
    }

    private static Vector3 GetVector3OrDefault(JObject element, string key, Vector3 defaultValue)
    {
        return element[key] is { } token ? token.GetVector3() : defaultValue;
    }
}
