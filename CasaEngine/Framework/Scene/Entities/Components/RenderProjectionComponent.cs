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
public class RenderProjectionComponent : SceneComponent, IEntityPolicyDefaultsProvider
{
    public RenderProjectionComponent()
    {
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public RenderProjectionComponent(Guid id) : base(id)
    {
    }

    public RenderProjectionComponent(RenderProjectionComponent other) : base(other)
    {
        SnapToPixel = other.SnapToPixel;
    }

    /// <summary>
    /// When set, the derived render position is snapped to the integer pixel grid through
    /// <see cref="SimulationSpacePolicy.SnapRenderPosition"/> before this component places itself.
    /// Off by default so no existing behaviour changes; the logical pose and the physics stay
    /// untouched either way. Meant for entities whose logical pose keeps moving fractionally every
    /// frame (e.g. a controller-driven mover), where a fractional render position beats the sprite's
    /// texel grid against the screen's and blurs it at non-unit zoom.
    /// </summary>
    [DefaultValue(false)]
    public bool SnapToPixel { get; set; }

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

        if (SnapToPixel)
        {
            renderPosition = policy.SnapRenderPosition(renderPosition);
        }

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

        var newLocalPosition = Vector3.Transform(renderPosition, Matrix.Invert(ambientMatrix));

        if (newLocalPosition != LocalTransform.Position)
        {
            LocalTransform.Position = newLocalPosition;

            //The world's spatial index only inspects the root and entity-level components of an entity
            //(Entity.GetBoundingBox), so a projection moving somewhere under the root must mark the root
            //itself dirty, including on the very first update after the entity was added: the box the
            //index stored at add time predates any projection.
            root.MarkBoundingBoxDirty();
        }
    }

    /// <summary>
    /// A render projection derives a world position from its entity root every update, so without physics
    /// driving it an entity carrying one still needs dynamic index maintenance and a tick every frame -
    /// otherwise <see cref="UpdateProjection"/> above would never run and the index would never learn the
    /// projected position moved.
    /// </summary>
    public void ApplyEntityPolicyDefaults(Entity owner, ref EntityPolicyDefaultsBuilder defaults)
    {
        defaults.Apply(EntityPolicySet.DynamicDefault);
    }
}
