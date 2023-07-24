using BulletSharp;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public class PhysicsDefinition
{
    public PhysicsType PhysicsType { get; set; }
    public float? AdditionalAngularDampingFactor { get; set; }
    public float? AdditionalAngularDampingThresholdSqr { get; set; }
    public bool? AdditionalDamping { get; set; }
    public float? AdditionalDampingFactor { get; set; }
    public float? AdditionalLinearDampingThresholdSqr { get; set; }
    public float? AngularDamping { get; set; }
    public Vector3? AngularFactor { get; set; }
    public float? AngularSleepingThreshold { get; set; }
    public CollisionShape? CollisionShape { get; set; }
    public float? Friction { get; set; }
    public float? LinearDamping { get; set; }
    public Vector3? LinearFactor { get; set; }
    public float? LinearSleepingThreshold { get; set; }
    public Vector3? LocalInertia { get; set; }
    public float? Mass { get; set; }
    public MotionState? MotionState { get; set; }
    public float? Restitution { get; set; }
    public float? RollingFriction { get; set; }
    public bool? ApplyGravity { get; set; }
    public Color? DebugColor { get; set; }
}