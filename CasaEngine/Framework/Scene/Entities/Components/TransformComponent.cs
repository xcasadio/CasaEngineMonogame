using CasaEngine.Core.Math.Geometry;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Inert scene component: no state and no visual of its own, usable as the root of an entity that only
/// needs to carry a pose. Typical use is a gameplay world whose root carries a pure logical pose (for
/// example <see cref="CasaEngine.Engine.Physics.TopDownElevationSimulationSpacePolicy"/>: X east, Y depth,
/// Z elevation) while a <see cref="RenderProjectionComponent"/> child carries the visuals.
/// </summary>
/// <remarks>
/// Update and Draw are intentionally not overridden: the propagation inherited from
/// <see cref="SceneComponent"/> is what makes children (a <see cref="RenderProjectionComponent"/>, a
/// <see cref="Components.CollisionComponent"/>, ...) tick and draw. "Inert" only means this component
/// itself holds no state and draws nothing.
/// </remarks>
public class TransformComponent : SceneComponent
{
    public TransformComponent()
    {
    }

    public TransformComponent(TransformComponent other) : base(other)
    {
    }

    public override TransformComponent Clone()
    {
        return new TransformComponent(this);
    }

    /// <summary>
    /// The union of the bounding boxes of the drawable descendants of this component (recursive; physics
    /// components are excluded, they live in the physical world, not in render space). Falls back to the
    /// inherited box (a small box around this component itself) when there is no drawable descendant.
    /// </summary>
    public override BoundingBox GetBoundingBox()
    {
        var result = BoundingBoxHelper.Create();
        var hasBox = false;

        for (int i = 0; i < Children.Count; i++)
        {
            AccumulateDrawableDescendantBounds(Children[i], ref result, ref hasBox);
        }

        return hasBox ? result : base.GetBoundingBox();
    }

    private static void AccumulateDrawableDescendantBounds(SceneComponent component, ref BoundingBox result, ref bool hasBox)
    {
        if (component is PhysicsBaseComponent)
        {
            return;
        }

        if (component is IComponentDrawable drawable)
        {
            result.ExpandBy(drawable.GetBoundingBox());
            hasBox = true;
        }

        for (int i = 0; i < component.Children.Count; i++)
        {
            AccumulateDrawableDescendantBounds(component.Children[i], ref result, ref hasBox);
        }
    }
}
