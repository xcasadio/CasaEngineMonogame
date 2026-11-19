using System.ComponentModel;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// The 3d camera of the engine (ADR-0149): a perspective camera posed by the transform of its component (ADR-0147).
/// Its view is the inverse of <see cref="SceneComponent.WorldMatrixNoScale"/>, parents included. It looks down its
/// local -Z axis with +Y up. <see cref="PictureOffset"/> shifts the picture without moving the point of view.
/// </summary>
[DisplayName("Perspective Camera")]
public class PerspectiveCameraComponent : CameraComponent
{
    /// <summary>Vertical field of view of a camera that does not set one, in radians (45 degrees).</summary>
    public const float DefaultFieldOfView = MathHelper.PiOver4;

    //  The world matrix the view was last computed from.
    private Matrix _viewWorldMatrix;
    private Vector2 _pictureOffset;
    private float _fieldOfView = DefaultFieldOfView;

    /// <summary>
    /// The vertical field of view, in radians. It is kept whatever the shape of the viewport: a wider viewport shows more
    /// on the sides, never less above and below (ADR-0095).
    /// </summary>
    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = Math.Clamp(value, 0.1f, MathHelper.Pi - 0.1f);
            _needToComputeProjectionMatrix = true;
        }
    }

    /// <summary>
    /// Shift of the picture, in fractions of the whole width and height of the viewport: a point drawn at pixel p, y down,
    /// is drawn at p + (X * width, Y * height). A positive X moves the picture to the right, a positive Y moves it down.
    /// The point of view does not move: the projection is off-centre.
    /// </summary>
    [SerializedProperty("picture_offset")]
    public Vector2 PictureOffset
    {
        get => _pictureOffset;
        set
        {
            _pictureOffset = value;
            _needToComputeProjectionMatrix = true;
        }
    }

    public PerspectiveCameraComponent()
    {
    }

    public PerspectiveCameraComponent(PerspectiveCameraComponent other) : base(other)
    {
        _fieldOfView = other._fieldOfView;
        _pictureOffset = other._pictureOffset;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        if (_fieldOfView <= 0f)
        {
            FieldOfView = DefaultFieldOfView;
        }

        _needToComputeProjectionMatrix = true;
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

    protected override void ComputeProjectionMatrix()
    {
        if (_pictureOffset == Vector2.Zero)
        {
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                _fieldOfView,
                _viewport.AspectRatio,
                _viewport.MinDepth,
                _viewport.MaxDepth);

            _needToComputeProjectionMatrix = false;
            return;
        }

        //  The window on the near plane, moved against the offset in x (the picture goes right when the window goes left)
        //  and with it in y (the screen y axis points down, the view y axis up).
        float near = _viewport.MinDepth;
        float halfHeight = near * MathF.Tan(FieldOfView * 0.5f);
        float halfWidth = halfHeight * _viewport.AspectRatio;
        float centreX = -_pictureOffset.X * 2f * halfWidth;
        float centreY = _pictureOffset.Y * 2f * halfHeight;

        _projectionMatrix = Matrix.CreatePerspectiveOffCenter(
            centreX - halfWidth,
            centreX + halfWidth,
            centreY - halfHeight,
            centreY + halfHeight,
            near,
            _viewport.MaxDepth);

        _needToComputeProjectionMatrix = false;
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

    public override void Load(JObject element)
    {
        base.Load(element);

        _fieldOfView = element["fieldOfView"].GetSingle();
    }
}
