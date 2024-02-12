using System.ComponentModel;

using BulletSharp;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Physics;

public class PhysicsDefinition
{
    public PhysicsType PhysicsType { get; set; }
    public float AdditionalAngularDampingFactor { get; set; } = 0.01f;
    public float AdditionalAngularDampingThresholdSqr { get; set; } = 0.01f;
    public bool AdditionalDamping { get; set; } = false;
    public float AdditionalDampingFactor { get; set; } = 0.005f;
    public float AdditionalLinearDampingThresholdSqr { get; set; } = 0.01f;
    public float AngularDamping { get; set; } = 0f;
    public Vector3 AngularFactor { get; set; } = Vector3.One;
    public float AngularSleepingThreshold { get; set; } = 1f;
    public CollisionShape? CollisionShape { get; set; }
    public float Friction { get; set; } = 0.5f;
    public float LinearDamping { get; set; } = 0f;
    public Vector3 LinearFactor { get; set; } = Vector3.One;
    public float LinearSleepingThreshold { get; set; } = 0.8f;
    public Vector3 LocalInertia { get; set; }
    public float Mass { get; set; } = 0f;

    [Browsable(false)]
    public MotionState? MotionState { get; set; }
    public float Restitution { get; set; } = 0f;
    public float RollingFriction { get; set; } = 0f;
    public bool ApplyGravity { get; set; } = true;
    public Color? DebugColor { get; set; }

    public PhysicsDefinition()
    {

    }

    public PhysicsDefinition(PhysicsDefinition other)
    {
        PhysicsType = other.PhysicsType;
        AdditionalAngularDampingFactor = other.AdditionalAngularDampingFactor;
        AdditionalAngularDampingThresholdSqr = other.AdditionalAngularDampingThresholdSqr;
        AdditionalDamping = other.AdditionalDamping;
        AdditionalDampingFactor = other.AdditionalDampingFactor;
        AdditionalLinearDampingThresholdSqr = other.AdditionalLinearDampingThresholdSqr;
        AngularDamping = other.AngularDamping;
        AngularFactor = other.AngularFactor;
        AngularSleepingThreshold = other.AngularSleepingThreshold;
        //CollisionShape = other.CollisionShape?.Clone();
        Friction = other.Friction;
        LinearDamping = other.LinearDamping;
        LinearFactor = other.LinearFactor;
        LinearSleepingThreshold = other.LinearSleepingThreshold;
        LocalInertia = other.LocalInertia;
        Mass = other.Mass;
        Restitution = other.Restitution;
        RollingFriction = other.RollingFriction;
        ApplyGravity = other.ApplyGravity;
        DebugColor = other.DebugColor;
    }

    public void Load(JObject element)
    {
        PhysicsType = element["physics_type"].GetEnum<PhysicsType>();
        AdditionalAngularDampingFactor = element["additional_angular_damping_factor"].GetSingle();
        AdditionalAngularDampingThresholdSqr = element["additional_angular_damping_threshold_sqr"].GetSingle();
        AdditionalDamping = element["additional_damping"].GetBoolean();
        AdditionalDampingFactor = element["additional_damping_factor"].GetSingle();
        AdditionalLinearDampingThresholdSqr = element["additional_linear_damping_threshold_sqr"].GetSingle();
        AngularDamping = element["angular_damping"].GetSingle();
        AngularFactor = element["angular_factor"].GetVector3();
        AngularSleepingThreshold = element["angular_sleeping_threshold"].GetSingle();
        Friction = element["friction"].GetSingle();
        LinearDamping = element["linear_damping"].GetSingle();
        LinearFactor = element["linear_factor"].GetVector3();
        LinearSleepingThreshold = element["linear_sleeping_threshold"].GetSingle();
        LocalInertia = element["local_inertia"].GetVector3();
        Mass = element["mass"].GetSingle();
        Restitution = element["restitution"].GetSingle();
        RollingFriction = element["rolling_friction"].GetSingle();
        ApplyGravity = element["apply_gravity"].GetBoolean();

        var debugColorElement = element["debug_color"];
        if (debugColorElement.GetString() != "null")
        {
            DebugColor = element["debug_color"].GetColor();
        }
    }

#if EDITOR

    public void Save(JObject jObject)
    {
        jObject.Add("physics_type", PhysicsType.ConvertToString());
        jObject.Add("additional_angular_damping_factor", AdditionalAngularDampingFactor);
        jObject.Add("additional_angular_damping_threshold_sqr", AdditionalAngularDampingThresholdSqr);
        jObject.Add("additional_damping", AdditionalDamping);
        jObject.Add("additional_damping_factor", AdditionalDampingFactor);
        jObject.Add("additional_linear_damping_threshold_sqr", AdditionalLinearDampingThresholdSqr);
        jObject.Add("angular_damping", AngularDamping);

        JObject newJObject = new();
        AngularFactor.Save(newJObject);
        jObject.Add("angular_factor", newJObject);

        jObject.Add("angular_sleeping_threshold", AngularSleepingThreshold);
        jObject.Add("friction", Friction);
        jObject.Add("linear_damping", LinearDamping);

        newJObject = new();
        LinearFactor.Save(newJObject);
        jObject.Add("linear_factor", newJObject);

        jObject.Add("linear_sleeping_threshold", LinearSleepingThreshold);

        newJObject = new();
        LocalInertia.Save(newJObject);
        jObject.Add("local_inertia", newJObject);

        jObject.Add("mass", Mass);
        jObject.Add("restitution", Restitution);
        jObject.Add("rolling_friction", RollingFriction);
        jObject.Add("apply_gravity", ApplyGravity);

        if (DebugColor != null)
        {
            newJObject = new();
            DebugColor.Value.Save(newJObject);
            jObject.Add("debug_color", newJObject);
        }
        else
        {
            jObject.Add("debug_color", "null");
        }
    }
#endif
}