using System.ComponentModel;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("ArcBall Camera")]
public class ArcBallCameraComponent : Camera3dComponent
{
    private Vector3 _target;
    private float _distance;
    private float _yaw, _pitch;

    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = value;
            _needToComputeViewMatrix = true;
            ComputeOrientation();
        }
    }

    public float Yaw
    {
        get => _yaw;
        set
        {
            _yaw = value;
            _needToComputeViewMatrix = true;
            ComputeOrientation();
        }
    }

    public Vector3 Direction
    {
        get
        {
            //R v R' where v = (0,0,-1,0)
            //The equation can be reduced because we know the following things:
            //  1.  We're using unit quaternions
            //  2.  The initial aspect does not change
            //The reduced form of the same equation follows
            var orientation = Orientation;

            var dir = Vector3.Zero;
            dir.X = -2.0f * (orientation.X * orientation.Z + orientation.W * orientation.Y);
            dir.Y = 2.0f * (orientation.W * orientation.X - orientation.Y * orientation.Z);
            dir.Z =
                orientation.X * orientation.X + orientation.Y * orientation.Y -
                (orientation.Z * orientation.Z + orientation.W * orientation.W);
            dir.Normalize();
            return dir;
        }
    }

    public Vector3 Right
    {
        get
        {
            //R v R' where v = (1,0,0,0)
            //The equation can be reduced because we know the following things:
            //  1.  We're using unit quaternions
            //  2.  The initial aspect does not change
            //The reduced form of the same equation follows
            var orientation = Orientation;

            var right = Vector3.Zero;
            right.X =
                orientation.X * orientation.X + orientation.W * orientation.W -
                (orientation.Z * orientation.Z + orientation.Y * orientation.Y);
            right.Y = 2.0f * (orientation.X * orientation.Y + orientation.Z * orientation.W);
            right.Z = 2.0f * (orientation.X * orientation.Z - orientation.Y * orientation.W);

            return right;
        }
    }
    /*
    public Vector3 Up
    {
        get
        {
            //R v R' where v = (0,1,0,0)
            //The equation can be reduced because we know the following things:
            //  1.  We're using unit quaternions
            //  2.  The initial aspect does not change
            //The reduced form of the same equation follows
            var orientation = Orientation;

            var up = Vector3.Zero;
            up.X = 2.0f * (orientation.X * orientation.Y - orientation.Z * orientation.W);
            up.Y =
                orientation.Y * orientation.Y + orientation.W * orientation.W -
                (orientation.Z * orientation.Z + orientation.X * orientation.X);
            up.Z = 2.0f * (orientation.Y * orientation.Z + orientation.X * orientation.W);
            return up;
        }
    }*/

    public Vector3 Target
    {
        get => _target;
        set
        {
            _needToComputeViewMatrix = true;
            _target = value;
        }
    }

    public float Distance
    {
        get => _distance;
        set
        {
            _distance = value;
            _needToComputeViewMatrix = true;
        }
    }

    public ArcBallCameraComponent()
    {
        _distance = 5.0f;
        _yaw = MathHelper.Pi;
        _pitch = 0.0f;
        _target = Vector3.Zero;
    }

    public ArcBallCameraComponent(ArcBallCameraComponent other) : base(other)
    {
        _distance = other._distance;
        _yaw = other._yaw;
        _pitch = other._pitch;
        _target = other._target;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        //orientation quaternion assumes a PI rotation so you're facing the "front"
        //of the model (looking down the +Z axis)
        //Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        ComputeOrientation();
    }

    public override ArcBallCameraComponent Clone()
    {
        return new ArcBallCameraComponent(this);
    }

    protected override void ComputeViewMatrix()
    {
        var position = _target - Direction * _distance;
        Position = position;
        _viewMatrix = Matrix.CreateLookAt(position, _target, Up);
    }

    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        SetCamera(position, target, Up);
    }

    /// <summary>
    /// Orbit directly upwards in Free camera or on
    /// the longitude line when roll constrained
    /// </summary>
    public void OrbitUp(float angle)
    {
        //update the pitch, constrained to vertical to avoid confusion
        _pitch = MathHelper.Clamp(_pitch - angle, -(MathHelper.PiOver2) + .0001f, (MathHelper.PiOver2) - .0001f);

        //create a new aspect based on pitch and yaw
        Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw) * Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        //normalize to reduce errors
        //orientation.Normalized(); ??

        _needToComputeViewMatrix = true;
    }

    /// <summary>
    /// Orbit towards the Right vector in Free camera
    /// or on the latitude line when roll constrained
    /// </summary>
    public void OrbitRight(float angle)
    {
        _yaw -= angle;

        //float mod yaw to avoid eventual precision errors
        //as we move away from 0
        _yaw %= MathHelper.TwoPi;
        Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw) * Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        Orientation.Normalize();

        _needToComputeViewMatrix = true;
    }

    /// <summary>
    /// Rotate around the Forward vector 
    /// in free-look camera only
    /// </summary>
    /*void RotateClockwise(float angle)
    {
        m_bRecomputeViewMatrix = true;

        Quaternion q1;

        q1.FromAxisAngle(Vector3.Forward(), angle);
        //rotate the aspect by the angle
        _orientation = _orientation * q1;
        //normalize to reduce errors
        //_orientation.Normalized();        
    }*/

    public void RotateTargetUp(float angle)
    {
        var rot = Quaternion.CreateFromAxisAngle(Right, angle);
        var dir = Direction * _distance;
        Vector3.Transform(ref dir, ref rot, out var vec);
        Target += vec - dir;

        _needToComputeViewMatrix = true;
    }

    public void RotateTargetRight(float angle)
    {
        var rot = Quaternion.CreateFromAxisAngle(Up, angle);
        var dir = Direction * _distance;
        Vector3.Transform(ref dir, ref rot, out var vec);
        Target += vec - dir;

        _needToComputeViewMatrix = true;
    }

    /// <summary>
    /// Places the camera at <paramref name="position"/> looking at <paramref name="target"/>, so that
    /// <see cref="CameraComponent.ViewMatrix"/> matches <c>Matrix.CreateLookAt(position, target, up)</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="up"/> is not used: an arc ball is defined by a yaw and a pitch only and carries no
    /// roll, so its up vector always stays in the vertical plane holding the view direction. The parameter
    /// is kept for source compatibility.
    /// </remarks>
    public void SetCamera(Vector3 position, Vector3 target, Vector3 up)
    {
        Vector3 dir = position - target;
        Target = target;

        //Distance is positive everywhere (constructor, Distance setter, editor viewport controller):
        //ComputeViewMatrix places the camera at Target - Direction * Distance, with Direction looking
        //from the camera towards the target. A negative distance here would mirror every orbit /
        //move control until the next Distance assignment flips the camera to the other side.
        Distance = dir.Length();

        if (Distance <= float.Epsilon)
        {
            return;
        }

        Vector3 zAxis = dir / Distance;

        //Direction(Yaw, Pitch) is (sin Yaw cos Pitch, sin Pitch, -cos Yaw cos Pitch) and must equal
        //-zAxis (camera -> target). Atan2 keeps the whole 360 degrees, including the x == 0 case where
        //a Math.Sign based reconstruction collapses to zero and mirrors the camera in Z.
        Yaw = MathF.Atan2(-zAxis.X, zAxis.Z);

        //sin Pitch = -zAxis.Y: looking down at the target from above gives a negative pitch.
        Pitch = -MathF.Asin(MathHelper.Clamp(zAxis.Y, -1f, 1f));
    }

    private void ComputeOrientation()
    {
        var q1 = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw);
        var q2 = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        Orientation = q1 * q2;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        _target = element["target"].GetVector3();
        _distance = element["distance"].GetSingle();
        _yaw = element["yaw"].GetSingle();
        _pitch = element["pitch"].GetSingle();
    }

}