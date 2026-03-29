using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public readonly record struct SteeringAgentKinematics(
    Vector3 Position,
    Vector3 Velocity,
    Vector3 Forward,
    Vector3 Right,
    float Mass,
    float MaxSpeed,
    float MaxForce,
    float MaxTurnRate)
{
    public float Speed => Velocity.Length();
}