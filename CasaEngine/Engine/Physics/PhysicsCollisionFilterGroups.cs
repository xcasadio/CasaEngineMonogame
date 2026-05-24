namespace CasaEngine.Engine.Physics;

[Flags]
public enum PhysicsCollisionFilterGroups
{
    None = 0,
    DefaultFilter = 1,
    StaticFilter = 2,
    KinematicFilter = 4,
    DebrisFilter = 8,
    SensorTrigger = 16,
    CharacterFilter = 32,
    AllFilter = -1
}