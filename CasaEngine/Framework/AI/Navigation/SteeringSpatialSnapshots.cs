using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public readonly struct SteeringObstacleSnapshot
{
    public SteeringObstacleSnapshot(Entity entity, Vector2 position, float radius)
    {
        Entity = entity;
        Position = position;
        Radius = radius;
    }

    public Entity Entity { get; }

    public Vector2 Position { get; }

    public float Radius { get; }
}

public readonly struct SteeringWallSnapshot
{
    public SteeringWallSnapshot(Entity entity, Vector2 start, Vector2 end, Vector2 normal, float thickness)
    {
        Entity = entity;
        Start = start;
        End = end;
        Normal = normal;
        Thickness = thickness;
    }

    public Entity Entity { get; }

    public Vector2 Start { get; }

    public Vector2 End { get; }

    public Vector2 Normal { get; }

    public float Thickness { get; }
}