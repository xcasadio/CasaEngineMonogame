using Microsoft.Xna.Framework;
using System.ComponentModel;
using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("ArcBall Camera")]
public class ArcBallCameraComponent : Camera3dComponent
{
    public static readonly int ComponentId = (int)ComponentIds.ArcBallCamera;

    private Vector3 _target;
    private float _distance;
    private float _yaw, _pitch;

    public float Pitch
    {
        get { return _pitch; }
        set
        {
            _pitch = value;
            _needToComputeViewMatrix = true;
            ComputeOrientation();
#if EDITOR
            OnPropertyChanged();
#endif

        }
    }

    public float Yaw
    {
        get { return _yaw; }
        set
        {
            _yaw = value;
            _needToComputeViewMatrix = true;
            ComputeOrientation();
#if EDITOR
            OnPropertyChanged();
#endif

        }
    }

    private Quaternion Orientation
    {
        get => Owner.Coordinates.LocalRotation;
        set => Owner.Coordinates.LocalRotation = value;
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
    }

    public override Vector3 Position => _target - Direction * _distance;

    public Vector3 Target
    {
        get { return _target; }
        set
        {
            _needToComputeViewMatrix = true;
            _target = value;
            UpdatePosition();
#if EDITOR
            OnPropertyChanged();
#endif

        }
    }

    public float Distance
    {
        get { return _distance; }
        set
        {
            _distance = value;
            _needToComputeViewMatrix = true;
            UpdatePosition();
#if EDITOR
            OnPropertyChanged();
#endif

        }
    }


    public ArcBallCameraComponent(Entity entity) : base(entity, ComponentId)
    {
        _distance = 5.0f;
        _yaw = MathHelper.Pi;
        _pitch = 0.0f;

        //orientation quaternion assumes a PI rotation so you're facing the "front"
        //of the model (looking down the +Z axis)
        Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        _target = Vector3.Zero;
        UpdatePosition();
    }

    public override void Update(float elapsedTime)
    {
        //Do nothing
    }

    protected override void ComputeViewMatrix()
    {
        var position = _target - Direction * _distance;
        _viewMatrix = Matrix.CreateLookAt(position, _target, Up);
    }

    /// <summary>
    /// Orbit directly upwards in Free camera or on
    /// the longitude line when roll constrained
    /// </summary>
    public void OrbitUp(float angle)
    {
        _needToComputeViewMatrix = true;

        //update the yaw
        _pitch -= angle;

        //constrain pitch to vertical to avoid confusion
        MathHelper.Clamp(_pitch, -(MathHelper.PiOver2) + .0001f, (MathHelper.PiOver2) - .0001f);

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
        _needToComputeViewMatrix = true;

        var rot = Quaternion.CreateFromAxisAngle(Right, angle);
        var dir = Direction * _distance;
        Vector3.Transform(ref dir, ref rot, out var vec);
        Target += vec - dir;

        _needToComputeViewMatrix = true;
    }

    public void RotateTargetRight(float angle)
    {
        _needToComputeViewMatrix = true;

        var rot = Quaternion.CreateFromAxisAngle(Up, angle);
        var dir = Direction * _distance;
        Vector3.Transform(ref dir, ref rot, out var vec);
        Target += vec - dir;

        _needToComputeViewMatrix = true;
    }

    public void SetCamera(Vector3 position, Vector3 target, Vector3 up)
    {
        Vector3 dir = position - target;
        Target = -target;
        Distance = -dir.Length();

        Vector3 zAxis = dir;
        zAxis.Normalize();
        up.Normalize();
        Vector3 xAxis = (Vector3.Cross(zAxis, up));
        xAxis.Normalize();
        Vector3 yAxis = (Vector3.Cross(xAxis, zAxis));
        yAxis.Normalize();
        xAxis = (Vector3.Cross(zAxis, yAxis));
        xAxis.Normalize();

        //Matrix m = Matrix.Identity;
        //m.Right = xAxis;
        //m.Forward = zAxis;
        //m.Up = yAxis;
        //m = Matrix.Transpose(m);

        //find the yaw of the direction on the x/z plane
        //and use the sign of the x-component since we have 360 degrees
        //of freedom
        Yaw = (float)(Math.Acos(-zAxis.Z) * Math.Sign(zAxis.X));

        //Get the pitch from the angle formed by the Up vector and the 
        //the forward direction, then subtracting PI / 2, since 
        //we pitch is zero at Forward, not Up.
        Pitch = (float)-(Math.Acos(Vector3.Dot(Vector3.Up, zAxis)) - MathHelper.PiOver2);
    }

    private void ComputeOrientation()
    {
        var q1 = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw);
        var q2 = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        Orientation = q1 * q2;
    }

    private void UpdatePosition()
    {
        Owner.Coordinates.LocalPosition = Position;
    }

    public override Component Clone(Entity owner)
    {
        var component = new ArcBallCameraComponent(owner);

        component._target = _target;
        component.Orientation = Orientation;
        component._distance = _distance;
        component._yaw = _yaw;
        component._pitch = _pitch;

        return component;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        _target = element.GetProperty("target").GetVector3();
        _distance = element.GetProperty("distance").GetSingle();
        _yaw = element.GetProperty("yaw").GetSingle();
        _pitch = element.GetProperty("pitch").GetSingle();
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        JObject newJObject = new();
        _target.Save(newJObject);
        jObject.Add("target", newJObject);
        jObject.Add("distance", _distance);
        jObject.Add("yaw", _yaw);
        jObject.Add("pitch", _pitch);
    }
#endif
}