using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringWallComponent : EntityComponent, ISteeringWallProvider
{
    public SteeringWallComponent()
    {
    }

    private SteeringWallComponent(SteeringWallComponent other)
        : base(other)
    {
        LocalStart = other.LocalStart;
        LocalEnd = other.LocalEnd;
        Thickness = other.Thickness;
    }

    public Vector2 LocalStart { get; set; }

    public Vector2 LocalEnd { get; set; }

    public float Thickness { get; set; } = 8.0f;

    Vector2 ISteeringWallProvider.SteeringWallStart => Transform(LocalStart);

    Vector2 ISteeringWallProvider.SteeringWallEnd => Transform(LocalEnd);

    Vector2 ISteeringWallProvider.SteeringWallNormal
    {
        get
        {
            Vector2 start = Transform(LocalStart);
            Vector2 end = Transform(LocalEnd);
            Vector2 direction = end - start;
            if (direction.LengthSquared() <= float.Epsilon)
            {
                return Vector2.UnitY;
            }

            direction.Normalize();
            return new Vector2(-direction.Y, direction.X);
        }
    }

    float ISteeringWallProvider.SteeringWallThickness => Thickness;

    public override EntityComponent Clone()
    {
        return new SteeringWallComponent(this);
    }

    private Vector2 Transform(Vector2 localPoint)
    {
        if (Owner?.RootComponent == null)
        {
            return localPoint;
        }

        Vector3 worldPoint = Vector3.Transform(new Vector3(localPoint, 0.0f), Owner.RootComponent.WorldMatrixNoScale);
        return new Vector2(worldPoint.X, worldPoint.Y);
    }
}