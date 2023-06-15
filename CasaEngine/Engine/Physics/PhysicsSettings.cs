using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics;

public class PhysicsSettings
{
    public PhysicsEngineFlags Flags = PhysicsEngineFlags.ContinuousCollisionDetection;

    /// The maximum number of simulations the the physics engine can run in a frame to compensate for slowdown
    public int MaxSubSteps = 1;

    /// The length in seconds of a physics simulation frame. The default is 0.016667 (one sixtieth of a second)
    public float FixedTimeStep = 1.0f / 60.0f;

    public Vector3 Gravity { get; set; } = new(0, -9.87f, 0f);
    public bool IsPhysics2dActivated { get; set; } = true;

    public PhysicsSettings()
    {
    }
}