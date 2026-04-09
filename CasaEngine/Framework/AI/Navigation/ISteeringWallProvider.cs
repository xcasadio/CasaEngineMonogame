using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public interface ISteeringWallProvider
{
    Vector2 SteeringWallStart { get; }

    Vector2 SteeringWallEnd { get; }

    Vector2 SteeringWallNormal { get; }

    float SteeringWallThickness { get; }
}