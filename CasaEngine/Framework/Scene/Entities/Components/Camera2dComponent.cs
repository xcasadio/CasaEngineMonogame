using System.ComponentModel;

using CasaEngine.Core.Logging;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Orthographic scene camera for world-space 2d rendering, posed by the transform of its component (ADR-0150): its
/// view is the inverse of <see cref="SceneComponent.WorldMatrixNoScale"/>, parents included. World units are pixels
/// (+Y up) and the camera looks down its local -Z axis; a roll turns the picture.
/// At <see cref="Zoom"/> = 1 a point of the plane <see cref="DefaultDistance"/> in front of the camera projects to the
/// same screen pixel as with the perspective trick this component replaces (a camera pushed back to
/// z = (viewport height / 2) / tan(fov / 2)), but without perspective distortion on the layers
/// placed at a different Z.
/// </summary>
[DisplayName("Camera 2d")]
public class Camera2dComponent : CameraComponent
{
    /// <summary>Smallest accepted zoom factor. Zero and negative values are clamped to it.</summary>
    public const float MinimumZoom = 0.0001f;

    /// <summary>
    /// Distance, along z, between a camera created in code and the plane z = 0, and between the camera and a point it is
    /// centred on by <see cref="CentreOn"/>.
    /// </summary>
    public const float DefaultDistance = 500.0f;

    //  The world matrix the view was last computed from.
    private Matrix _viewWorldMatrix;
    private float _zoom = 1.0f;
    private bool _pixelSnap;

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
    /// When enabled, the camera is snapped to the texel grid (step 1 / <see cref="Zoom"/>) in x and y when the
    /// view matrix is computed. The transform of the component is never modified.
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
        LocalPosition = new Vector3(0.0f, 0.0f, DefaultDistance);
    }

    public Camera2dComponent(Camera2dComponent other) : base(other)
    {
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

    protected override bool IsViewOutOfDate()
    {
        return WorldMatrixNoScale != _viewWorldMatrix;
    }

    protected override void ComputeViewMatrix()
    {
        _viewWorldMatrix = WorldMatrixNoScale;
        Matrix world = _viewWorldMatrix;

        if (_pixelSnap)
        {
            var step = 1.0f / _zoom;
            world.M41 = MathF.Round(world.M41 / step) * step;
            world.M42 = MathF.Round(world.M42 / step) * step;
        }

        _viewMatrix = Matrix.Invert(world);
    }

    /// <summary>
    /// Places the camera in front of <paramref name="point"/>, <see cref="DefaultDistance"/> away along z, so that the
    /// point is at the centre of the picture. It writes the local position: the point is a world position only for a
    /// camera with no parent. The orientation is not changed.
    /// </summary>
    public void CentreOn(Vector3 point)
    {
        LocalPosition = new Vector3(point.X, point.Y, point.Z + DefaultDistance);
    }

    /// <summary>Centres the camera on <paramref name="target"/>; <paramref name="position"/> is not used.</summary>
    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        CentreOn(target);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        Zoom = element["zoom"].GetSingle();
        _pixelSnap = element["pixel_snap"].GetBoolean();
        _needToComputeViewMatrix = true;

        if (element.ContainsKey("target"))
        {
            //  ADR-0150: the pose comes from the transform; a file written before that still carries the key.
            Logs.WriteError(
                $"Camera2dComponent '{element["name"]}' ({element["id"]}): the key 'target' is no longer read. The camera is posed by its transform: write (target.x, target.y, target.z + {DefaultDistance}) in its position and remove the key.");
        }
    }
}
