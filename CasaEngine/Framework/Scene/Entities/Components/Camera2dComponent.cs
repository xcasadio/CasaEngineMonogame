using System.ComponentModel;

using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Orthographic scene camera for world-space 2d rendering.
/// World units are pixels (+Y up), the camera looks along -Z at <see cref="Target"/>.
/// At <see cref="Zoom"/> = 1 a point of the plane Z = Target.Z projects to the same screen pixel as
/// with the perspective trick this component replaces (a camera pushed back to
/// z = (viewport height / 2) / tan(fov / 2)), but without perspective distortion on the layers
/// placed at a different Z.
/// </summary>
[DisplayName("Camera 2d")]
public class Camera2dComponent : CameraComponent
{
    /// <summary>Smallest accepted zoom factor. Zero and negative values are clamped to it.</summary>
    public const float MinimumZoom = 0.0001f;

    private const float DistanceToTargetPlane = 500.0f;

    private Vector3 _target;
    private float _zoom = 1.0f;
    private bool _pixelSnap;

    /// <summary>Point of the scene the camera is centered on, in world units.</summary>
    public Vector3 Target
    {
        get => _target;
        set
        {
            _target = value;
            _needToComputeViewMatrix = true;
        }
    }

    /// <summary>Magnification factor. The orthographic volume is the viewport size divided by this value.</summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = MathF.Max(value, MinimumZoom);
            _needToComputeProjectionMatrix = true;
            _needToComputeViewMatrix = true;
        }
    }

    /// <summary>
    /// When enabled, the camera is snapped to the texel grid (step 1 / <see cref="Zoom"/>) when the
    /// view matrix is computed. <see cref="Target"/> itself is never modified.
    /// </summary>
    public bool PixelSnap
    {
        get => _pixelSnap;
        set
        {
            _pixelSnap = value;
            _needToComputeViewMatrix = true;
        }
    }

    public Camera2dComponent()
    {
    }

    public Camera2dComponent(Camera2dComponent other) : base(other)
    {
        _target = other._target;
        _zoom = other._zoom;
        _pixelSnap = other._pixelSnap;
    }

    public override Camera2dComponent Clone()
    {
        return new Camera2dComponent(this);
    }

    protected override void ComputeProjectionMatrix()
    {
        _projectionMatrix = Matrix.CreateOrthographic(
            _viewport.Width / _zoom,
            _viewport.Height / _zoom,
            _viewport.MinDepth,
            _viewport.MaxDepth);

        _needToComputeProjectionMatrix = false;
    }

    protected override void ComputeViewMatrix()
    {
        var target = _target;

        if (_pixelSnap)
        {
            var step = 1.0f / _zoom;
            target.X = MathF.Round(target.X / step) * step;
            target.Y = MathF.Round(target.Y / step) * step;
        }

        var position = new Vector3(target.X, target.Y, target.Z + DistanceToTargetPlane);
        Position = position;
        _viewMatrix = Matrix.CreateLookAt(position, target, Vector3.Up);
        IsBoundingBoxDirty = true;
    }

    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        Target = target;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        _target = element["target"].GetVector3();
        Zoom = element["zoom"].GetSingle();
        _pixelSnap = element["pixel_snap"].GetBoolean();
        _needToComputeViewMatrix = true;
    }
}
