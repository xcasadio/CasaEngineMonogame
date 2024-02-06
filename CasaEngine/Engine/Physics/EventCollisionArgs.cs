using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.Engine.Physics;

public class EventCollisionArgs : EventArgs
{
    public Collision Collision { get; }
    public EntityComponent Component { get; }

    public EventCollisionArgs(Collision collision, EntityComponent component)
    {
        Collision = collision;
        Component = component;
    }
}