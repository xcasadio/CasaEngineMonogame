using System.ComponentModel;
using System.Text.Json;
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

    public void Load(JsonElement element)
    {
        PhysicsType = element.GetJsonPropertyByName("physics_type").Value.GetEnum<PhysicsType>();
        AdditionalAngularDampingFactor = element.GetJsonPropertyByName("additional_angular_damping_factor").Value.GetSingle();
        AdditionalAngularDampingThresholdSqr = element.GetJsonPropertyByName("additional_angular_damping_threshold_sqr").Value.GetSingle();
        AdditionalDamping = element.GetJsonPropertyByName("additional_damping").Value.GetBoolean();
        AdditionalDampingFactor = element.GetJsonPropertyByName("additional_damping_factor").Value.GetSingle();
        AdditionalLinearDampingThresholdSqr = element.GetJsonPropertyByName("additional_linear_damping_threshold_sqr").Value.GetSingle();
        AngularDamping = element.GetJsonPropertyByName("angular_damping").Value.GetSingle();
        AngularFactor = element.GetJsonPropertyByName("angular_factor").Value.GetVector3();
        AngularSleepingThreshold = element.GetJsonPropertyByName("angular_sleeping_threshold").Value.GetSingle();
        Friction = element.GetJsonPropertyByName("friction").Value.GetSingle();
        LinearDamping = element.GetJsonPropertyByName("linear_damping").Value.GetSingle();
        LinearFactor = element.GetJsonPropertyByName("linear_factor").Value.GetVector3();
        LinearSleepingThreshold = element.GetJsonPropertyByName("linear_sleeping_threshold").Value.GetSingle();
        LocalInertia = element.GetJsonPropertyByName("local_inertia").Value.GetVector3();
        Mass = element.GetProperty("mass").GetSingle();
        Restitution = element.GetJsonPropertyByName("restitution").Value.GetSingle();
        RollingFriction = element.GetJsonPropertyByName("rolling_friction").Value.GetSingle();
        ApplyGravity = element.GetJsonPropertyByName("apply_gravity").Value.GetBoolean();

        var debugColorElement = element.GetProperty("debug_color");
        if (debugColorElement.GetString() != "null")
        {
            DebugColor = element.GetJsonPropertyByName("debug_color").Value.GetColor();
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