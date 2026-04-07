using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;

namespace CasaEngine.Framework.Scene.Entities.Components;

public interface ICollideableComponent
{
    public Entity? Owner { get; }
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; }
}