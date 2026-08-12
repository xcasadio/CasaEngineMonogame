using System.ComponentModel;
using CasaEngine.Engine.Physics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Attachment point of the pure visuals of an entity in a world whose render space is not its simulation
/// space. Every update it places itself at
/// <see cref="SimulationSpacePolicy.DeriveRenderPosition"/> of the logical position of the entity root, so
/// its children render at the projected position while the collision bodies of the entity stay in the
/// logical space of the root.
/// The mapping is translation only, and it is inert under an identity policy (zero offset).
/// </summary>
/// <remarks>
/// A component reads the transform of its ancestors lazily (see <see cref="SceneComponent.WorldMatrixNoScale"/>),
/// and this component projects before it updates its children, so it always publishes the position of the
/// root of the same frame. The only placement constraint is the natural one: it must be a descendant of
/// the entity root, not the root itself.
/// An <see cref="AnimatedSpriteComponent"/> is safe here: it places the bodies of its collision timeline
/// on the logical pose of the entity root. A <see cref="StaticSpriteComponent"/> carrying authored
/// collision volumes is not: it builds its bodies from its own world transform, so those volumes would
/// land in render space instead of the logical space of the simulation.
/// </remarks>
[DisplayName("Render Projection")]
public class RenderProjectionComponent : SceneComponent
{
    public RenderProjectionComponent()
    {
    }

    public RenderProjectionComponent(RenderProjectionComponent other) : base(other)
    {
    }

    public override RenderProjectionComponent Clone()
    {
        return new RenderProjectionComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        UpdateProjection();
        base.Update(elapsedTime);
    }

    /// <summary>
    /// Places this component so that its world position is the render position derived from the logical
    /// position of the entity root.
    /// </summary>
    public void UpdateProjection()
    {
        var root = Owner?.RootComponent;
        var policy = Owner?.World?.PhysicsWorld?.SpacePolicy;

        if (root == null || policy == null || ReferenceEquals(root, this))
        {
            return;
        }

        var renderPosition = policy.DeriveRenderPosition(root.WorldMatrixNoScale.Translation);

        //Everything WorldMatrixNoScale applies on top of the local matrix of this component.
        var ambientMatrix = Matrix.Identity;

        if (Parent != null)
        {
            ambientMatrix *= Parent.WorldMatrixNoScale;
        }

        if (Owner?.Parent?.RootComponent != null)
        {
            ambientMatrix *= Owner.Parent.RootComponent.WorldMatrixNoScale;
        }

        LocalTransform.Position = Vector3.Transform(renderPosition, Matrix.Invert(ambientMatrix));
    }
}
