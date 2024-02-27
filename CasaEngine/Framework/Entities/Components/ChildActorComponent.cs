using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public class ChildActorComponent : SceneComponent
{
    private Entity? _actor;

    private Entity? Actor
    {
        get => _actor;
        set
        {
            _actor = value;
            IsBoundingBoxDirty = true;
        }
    }

    public ChildActorComponent()
    {
    }

    public ChildActorComponent(ChildActorComponent other) : base(other)
    {
        Actor = other.Actor;
    }

    public override ChildActorComponent Clone()
    {
        return new ChildActorComponent(this);
    }

    public override BoundingBox GetBoundingBox()
    {
        return Actor?.RootComponent?.BoundingBox ?? new BoundingBox();
    }
}