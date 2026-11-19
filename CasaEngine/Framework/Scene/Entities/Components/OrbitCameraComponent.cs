using System.ComponentModel;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Makes the camera of its entity orbit a target (ADR-0148): from a target, a distance, a yaw and a pitch it writes the
/// local transform of the camera component, at <c>Target - Direction * Distance</c>, looking at the target, without roll.
/// A negative distance is accepted: the camera then stands beyond the target along <see cref="Direction"/> and still
/// looks at the target.
/// <para/>
/// It writes at each change of one of its values, when its entity is initialised and when it joins a world, never per
/// frame. It owns the pose: a pose written directly on the camera is overwritten at its next write.
/// </summary>
[DisplayName("Orbit Camera")]
public class OrbitCameraComponent : EntityComponent
{
    private Vector3 _target;
    private float _distance = 5.0f;
    private float _yaw = MathHelper.Pi;
    private float _pitch;

    /// <summary>The point the camera looks at and turns around.</summary>
    public Vector3 Target
    {
        get => _target;
        set
        {
            _target = value;
            ApplyToCamera();
        }
    }

    /// <summary>Distance from the camera to the target.</summary>
    public float Distance
    {
        get => _distance;
        set
        {
            _distance = value;
            ApplyToCamera();
        }
    }

    /// <summary>Angle around the vertical axis, in radians.</summary>
    public float Yaw
    {
        get => _yaw;
        set
        {
            _yaw = value;
            ApplyToCamera();
        }
    }

    /// <summary>Angle above or below the horizon, in radians: negative when the camera looks down at the target.</summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = value;
            ApplyToCamera();
        }
    }

    /// <summary>Unit vector from the camera to the target: (sin Yaw cos Pitch, sin Pitch, -cos Yaw cos Pitch).</summary>
    public Vector3 Direction => Vector3.Transform(Vector3.Forward, ComputeOrientation());

    /// <summary>Unit vector to the right of the camera.</summary>
    public Vector3 Right => Vector3.Transform(Vector3.Right, ComputeOrientation());

    public OrbitCameraComponent()
    {
    }

    public OrbitCameraComponent(OrbitCameraComponent other) : base(other)
    {
        _target = other._target;
        _distance = other._distance;
        _yaw = other._yaw;
        _pitch = other._pitch;
    }

    public override OrbitCameraComponent Clone()
    {
        return new OrbitCameraComponent(this);
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
        ApplyToCamera();
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        ApplyToCamera();
    }

    /// <summary>
    /// Places the camera at <paramref name="position"/> looking at <paramref name="target"/>: the distance, the yaw and
    /// the pitch are deduced from the two points. A position equal to the target only moves the target.
    /// </summary>
    public void SetCamera(Vector3 position, Vector3 target)
    {
        Vector3 fromTarget = position - target;
        _target = target;
        _distance = fromTarget.Length();

        if (_distance > float.Epsilon)
        {
            Vector3 zAxis = fromTarget / _distance;

            //  Direction(Yaw, Pitch) must equal -zAxis (camera towards target). Atan2 keeps the whole turn.
            _yaw = MathF.Atan2(-zAxis.X, zAxis.Z);
            _pitch = -MathF.Asin(MathHelper.Clamp(zAxis.Y, -1f, 1f));
        }

        ApplyToCamera();
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        _target = element["target"].GetVector3();
        _distance = element["distance"].GetSingle();
        _yaw = element["yaw"].GetSingle();
        _pitch = element["pitch"].GetSingle();
    }

    private Quaternion ComputeOrientation()
    {
        return Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw) * Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
    }

    //  The camera is looked up at each write: a clone must pose the camera of its own entity. A camera added to an
    //  initialised entity after the orbit is posed at the next write.
    private void ApplyToCamera()
    {
        CameraComponent camera = Owner?.GetComponent<CameraComponent>();
        if (camera == null)
        {
            return;
        }

        Quaternion orientation = ComputeOrientation();
        Vector3 direction = Vector3.Transform(Vector3.Forward, orientation);

        //  The camera always looks at the target. With a negative distance it stands on the other side of the target,
        //  along Direction, and so looks back along -Direction: the orientation turned half a turn about its own up axis.
        camera.LocalOrientation = _distance < 0f
            ? orientation * Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi)
            : orientation;
        camera.LocalPosition = _target - direction * _distance;
    }
}
