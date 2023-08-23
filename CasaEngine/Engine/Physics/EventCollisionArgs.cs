using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Engine.Physics;

public class EventCollisionArgs : EventArgs
{
    public Collision Collision { get; }
    public Component Component { get; }

    public EventCollisionArgs(Collision collision, Component component)
    {
        Collision = collision;
        Component = component;
    }
}