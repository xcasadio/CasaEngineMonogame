namespace CasaEngine.Engine.Physics;

[Flags]
public enum PhysicsDebugDrawModes
{
    NoDebug = 0,
    DrawWireframe = 1,
    DrawAabb = 2,
    DrawContactPoints = 8,
    MaxDebugDrawMode = DrawWireframe | DrawContactPoints
}
