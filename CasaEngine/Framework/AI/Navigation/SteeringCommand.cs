using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public readonly record struct SteeringCommand(
    Vector3 LinearForce,
    Vector3 DesiredVelocity,
    Vector3 DesiredFacing,
    SteeringOutputMode OutputMode)
{
    public static SteeringCommand None(SteeringOutputMode outputMode)
    {
        return new SteeringCommand(Vector3.Zero, Vector3.Zero, Vector3.Forward, outputMode);
    }
}