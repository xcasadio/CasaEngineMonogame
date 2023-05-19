using System.Runtime.Serialization;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics2D;

public class Physics3dSettings
{
    public PhysicsEngineFlags Flags = PhysicsEngineFlags.ContinuousCollisionDetection;

    /// The maximum number of simulations the the physics engine can run in a frame to compensate for slowdown
    public int MaxSubSteps = 1;

    /// The length in seconds of a physics simulation frame. The default is 0.016667 (one sixtieth of a second)
    public float FixedTimeStep = 1.0f / 60.0f;

    public Vector3 Gravity { get; set; }

    public Physics3dSettings()
    {
    }
}