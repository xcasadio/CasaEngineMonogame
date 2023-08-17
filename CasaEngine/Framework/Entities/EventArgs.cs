using CasaEngine.Engine.Physics;

namespace CasaEngine.Framework.Entities;

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