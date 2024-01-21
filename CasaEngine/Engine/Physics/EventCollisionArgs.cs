using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.Engine.Physics;

public class EventCollisionArgs : EventArgs
{
    public Collision Collision { get; }
    public ActorComponent Component { get; }

    public EventCollisionArgs(Collision collision, ActorComponent component)
    {
        Collision = collision;
        Component = component;
    }
}