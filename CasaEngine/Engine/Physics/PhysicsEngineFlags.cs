namespace CasaEngine.Engine.Physics;

[Flags]
public enum PhysicsEngineFlags
{
    None = 0x0,

    /// <summary>Not implemented by the Bepu backend.</summary>
    CollisionsOnly = 0x1,

    /// <summary>Reserved.</summary>
    MultiThreaded = 0x4,

    ContinuousCollisionDetection = 0x10,
}
