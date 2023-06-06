using CasaEngine.Engine.Physics;

namespace CasaEngine.Framework.Entities.Components;

public interface ICollideableComponent
{
    public Entity Owner { get; }
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; }

    void OnHit(Collision collision);
    void OnHitEnded(Collision collision);
}