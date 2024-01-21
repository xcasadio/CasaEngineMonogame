using CasaEngine.Engine.Physics;

namespace CasaEngine.Framework.SceneManagement.Components;

public interface ICollideableComponent
{
    public AActor? Owner { get; }
    public PhysicsType PhysicsType { get; }
    public HashSet<Collision> Collisions { get; }
}