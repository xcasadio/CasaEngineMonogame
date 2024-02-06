using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.SceneManagement.Components;

public interface ICollideableComponent
{
    public Entity? Owner { get; }
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; }
}