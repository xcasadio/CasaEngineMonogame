using System.ComponentModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// A perspective camera posed by the transform of its component (ADR-0144): its view is the inverse of
/// <see cref="SceneComponent.WorldMatrixNoScale"/>, parents included. It looks down its local -Z axis with +Y up.
/// </summary>
[DisplayName("Perspective Camera")]
public class PerspectiveCameraComponent : Camera3dComponent
{
    //  The world matrix the view was last computed from.
    private Matrix _viewWorldMatrix;

    public PerspectiveCameraComponent()
    {
    }

    public PerspectiveCameraComponent(PerspectiveCameraComponent other) : base(other)
    {
    }

    public override PerspectiveCameraComponent Clone()
    {
        return new PerspectiveCameraComponent(this);
    }

    protected override bool IsViewOutOfDate()
    {
        return WorldMatrixNoScale != _viewWorldMatrix;
    }

    protected override void ComputeViewMatrix()
    {
        _viewWorldMatrix = WorldMatrixNoScale;
        _viewMatrix = Matrix.Invert(_viewWorldMatrix);
    }

    /// <summary>
    /// Places the camera at <paramref name="position"/> and turns it towards <paramref name="target"/>, with
    /// <see cref="Vector3.Up"/> for up and no roll. Both are written to the local transform: they are world
    /// coordinates only for a camera with no parent.
    /// </summary>
    /// <exception cref="ArgumentException">The target is at the position, or straight above or below it.</exception>
    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        var direction = target - position;
        if (direction.LengthSquared() < 1e-12f)
        {
            throw new ArgumentException("The target of a camera cannot be its position.", nameof(target));
        }

        direction.Normalize();
        if (MathF.Abs(Vector3.Dot(direction, Vector3.Up)) > 0.99999f)
        {
            throw new ArgumentException("A camera cannot be aimed straight up or down with Vector3.Up for up.", nameof(target));
        }

        var world = Matrix.Invert(Matrix.CreateLookAt(position, target, Vector3.Up));
        LocalPosition = position;
        LocalOrientation = Quaternion.CreateFromRotationMatrix(world);
    }
}
