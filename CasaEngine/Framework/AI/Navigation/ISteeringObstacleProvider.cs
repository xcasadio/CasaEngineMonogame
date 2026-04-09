using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public interface ISteeringObstacleProvider
{
    Vector2 SteeringObstaclePosition { get; }

    float SteeringObstacleRadius { get; }
}