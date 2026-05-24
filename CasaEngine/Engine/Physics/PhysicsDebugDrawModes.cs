namespace CasaEngine.Engine.Physics;

[Flags]
public enum PhysicsDebugDrawModes
{
    NoDebug = 0,
    DrawWireframe = 1,
    DrawAabb = 2,
    DrawFeaturesText = 4,
    DrawContactPoints = 8,
    NoDeactivation = 16,
    NoHelpText = 32,
    DrawText = 64,
    ProfileTimings = 128,
    EnableSatComparison = 256,
    DisableBulletLcp = 512,
    EnableCcd = 1024,
    DrawConstraints = 2048,
    DrawConstraintLimits = 4096,
    FastWireframe = 8192,
    DrawNormals = 16384,
    MaxDebugDrawMode = -1
}