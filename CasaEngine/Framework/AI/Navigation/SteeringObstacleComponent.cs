using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringObstacleComponent : EntityComponent, ISteeringObstacleProvider
{
    public SteeringObstacleComponent()
    {
    }

    private SteeringObstacleComponent(SteeringObstacleComponent other)
        : base(other)
    {
        Radius = other.Radius;
    }

    public float Radius { get; set; }

    Vector2 ISteeringObstacleProvider.SteeringObstaclePosition => new(Owner?.RootComponent?.Position.X ?? 0.0f, Owner?.RootComponent?.Position.Y ?? 0.0f);

    float ISteeringObstacleProvider.SteeringObstacleRadius => Radius;

    public override EntityComponent Clone()
    {
        return new SteeringObstacleComponent(this);
    }
}