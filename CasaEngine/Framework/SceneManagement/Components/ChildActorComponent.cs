using CasaEngine.Framework.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement.Components;

public class ChildActorComponent : SceneComponent
{
    private AActor? _actor;

    private AActor? Actor
    {
        get => _actor;
        set
        {
            _actor = value;
            IsWorldMatrixChange = true;
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