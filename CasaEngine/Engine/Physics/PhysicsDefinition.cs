using System.ComponentModel;
using BulletSharp;
using Microsoft.Xna.Framework;

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
    public float? Mass { get; set; } = 0f;

    [Browsable(false)]
    public MotionState? MotionState { get; set; }
    public float Restitution { get; set; } = 0f;
    public float RollingFriction { get; set; } = 0f;
    public bool ApplyGravity { get; set; } = true;
    public Color? DebugColor { get; set; }
}