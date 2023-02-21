using Microsoft.Xna.Framework;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Game;
using Keyboard = Microsoft.Xna.Framework.Input.Keyboard;
using Mouse = Microsoft.Xna.Framework.Input.Mouse;

namespace CasaEngine.Framework.Entities.Components;

public class ArcBallCameraComponent : Camera3dComponent
{
    public static readonly int ComponentId = (int)ComponentIds.ArcBallCamera;

    private Vector3 _target;
    private float _distance;
    private Quaternion _orientation;
    private float _inputTurnRate;
    private float _yaw, _pitch;
    private InputComponent _inputComponent;

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

    private void ComputeOrientation()
    {
        var q1 = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw);
        var q2 = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        _orientation = q1 * q2;
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
            var dir = Vector3.Zero;
            dir.X = -2.0f * (_orientation.X * _orientation.Z + _orientation.W * _orientation.Y);
            dir.Y = 2.0f * (_orientation.W * _orientation.X - _orientation.Y * _orientation.Z);
            dir.Z =
                _orientation.X * _orientation.X + _orientation.Y * _orientation.Y -
                (_orientation.Z * _orientation.Z + _orientation.W * _orientation.W);
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
            var right = Vector3.Zero;
            right.X =
                _orientation.X * _orientation.X + _orientation.W * _orientation.W -
                (_orientation.Z * _orientation.Z + _orientation.Y * _orientation.Y);
            right.Y = 2.0f * (_orientation.X * _orientation.Y + _orientation.Z * _orientation.W);
            right.Z = 2.0f * (_orientation.X * _orientation.Z - _orientation.Y * _orientation.W);

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
            var up = Vector3.Zero;
            up.X = 2.0f * (_orientation.X * _orientation.Y - _orientation.Z * _orientation.W);
            up.Y =
                _orientation.Y * _orientation.Y + _orientation.W * _orientation.W -
                (_orientation.Z * _orientation.Z + _orientation.X * _orientation.X);
            up.Z = 2.0f * (_orientation.Y * _orientation.Z + _orientation.X * _orientation.W);
            return up;
        }
    }

    public Vector3 Position
    {
        get => _target - Direction * _distance;
        set => SetCamera(value, _target, Up);
    }

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

    public float InputDistanceRate { get; set; }

    public float InputDisplacementRate { get; set; }

    public ArcBallCameraComponent(Entity entity) : base(entity, ComponentId)
    {
        _distance = 5.0f;
        InputDistanceRate = 3.0f;
        _inputTurnRate = 0.3f;
        InputDisplacementRate = 10.0f;
        _yaw = MathHelper.Pi;
        _pitch = 0.0f;

        //orientation quaternion assumes a PI rotation so you're facing the "front"
        //of the model (looking down the +Z axis)
        _orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        _target = Vector3.Zero;
    }

    public override void Initialize()
    {
        _inputComponent = Game.Engine.Instance.Game.GetGameComponent<InputComponent>();
        base.Initialize();
    }

    public override void Update(float elapsedTime)
    {
        var rightAxis = 0.0f;
        var upAxis = 0.0f;
        var forwardAxis = 0.0f;
        var horizontalOrbit = 0.0f;
        var verticalOrbit = 0.0f;
        var rollOrbit = 0.0f;
        var zoom = 0.0f;

        const float step = 1.0f;

        //Keyboard
        if (Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            rightAxis = step;
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            rightAxis = -step;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            forwardAxis = step;
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            forwardAxis = -step;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.PageUp))
        {
            upAxis = step;
        }
        else if (Keyboard.GetState().IsKeyDown(Keys.PageDown))
        {
            upAxis = -step;
        }

        if (Mouse.GetState().RightButton == ButtonState.Pressed)
        {

            horizontalOrbit = -_inputComponent.MouseXMovement;
            verticalOrbit = -_inputComponent.MouseYMovement;
        }

        //Touch
        //if (_inputComponent.IsTouchMove(0) == true)
        //{
        //    horizontalOrbit = -Game.Instance().GetInput().TouchMoveDeltaX(0);
        //    verticalOrbit = -Game.Instance().GetInput().TouchMoveDeltaY(0);
        //}

        HandleControls(elapsedTime, rightAxis, upAxis, forwardAxis, horizontalOrbit, verticalOrbit, rollOrbit, zoom);
    }

    /**
     * <param name="gameTime"></param>
     * <param name="horizontalAxis_"> value between -1.0 and 1.0 </param>
     * <param name="verticalAxis_"> value between -1.0 and 1.0 </param>
     * <param name="rollAxis_"> value between -1.0 and 1.0 </param>
     * <param name="zoom"> zoom value </param>
    */
    private void HandleControls(float elapsedTime, float rightAxis_, float upAxis_, float forwardAxis_, float horizontalOrbit_, float verticalOrbit_,
        float rollOrbit_, float zoom_)
    {
        var r = rightAxis_ * elapsedTime * InputDisplacementRate;
        var u = upAxis_ * elapsedTime * InputDisplacementRate;
        var f = forwardAxis_ * elapsedTime * InputDisplacementRate;

        var dH = horizontalOrbit_ * elapsedTime * _inputTurnRate;
        var dV = verticalOrbit_ * elapsedTime * _inputTurnRate;
        //float dR = rollOrbit_ * elapsedTime * _inputTurnRate;

        if (dH != 0.0f)
        {
            RotateTargetRight(dH);
            OrbitRight(dH);
        }

        if (dV != 0.0f)
        {
            RotateTargetUp(dV);
            OrbitUp(-dV);
        }
        //if ( dR != 0.0f ) RotateClockwise( dR );

        //decrease distance to target
        if (zoom_ != 0.0f)
        {
            _distance += zoom_ * elapsedTime * InputDistanceRate;

            if (_distance < 0.001f)
            {
                _distance = 0.001f;
                _needToComputeViewMatrix = true;
            }
        }

        if (r != 0.0f || u != 0.0f || f != 0.0f)
        {
            var pos = Target + Right * r + Up * u + Direction * f;
            Target = pos;
        }
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

        Quaternion q1, q2;

        //update the yaw
        _pitch -= angle;

        //constrain pitch to vertical to avoid confusion
        MathHelper.Clamp(_pitch, -(MathHelper.PiOver2) + .0001f, (MathHelper.PiOver2) - .0001f);

        //create a new aspect based on pitch and yaw
        _orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw) *
                                             Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
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
        _needToComputeViewMatrix = true;

        Quaternion q1, q2;

        //update the yaw
        _yaw -= angle;

        //float mod yaw to avoid eventual precision errors
        //as we move away from 0
        _yaw = _yaw % MathHelper.TwoPi;

        //create a new aspect based on pitch and yaw
        _orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, -_yaw) *
                                Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        //normalize to reduce errors
        _orientation.Normalize();

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
        _target += vec - dir;

        _needToComputeViewMatrix = true;
    }

    public void RotateTargetRight(float angle)
    {
        _needToComputeViewMatrix = true;

        var rot = Quaternion.CreateFromAxisAngle(Up, angle);
        var dir = Direction * _distance;
        Vector3.Transform(ref dir, ref rot, out var vec);
        _target += vec - dir;

        _needToComputeViewMatrix = true;
    }

    public void SetCamera(Vector3 position, Vector3 target, Vector3 up)
    {
        var temp = Matrix.CreateLookAt(position, target, up);
        temp = Matrix.Invert(temp);
        _orientation = Quaternion.CreateFromRotationMatrix(temp);

        Target = target;
        Distance = (target - position).Length();

        //When setting a new eye-view direction 
        //in one of the gamble-locked modes, the yaw and
        //pitch gimble must be calculated.

        //first, get the direction projected on the x/z plane
        var dir = (target - position);//Direction;
        dir.Y = 0.0f;
        if (dir.Length() == 0.0f)
        {
            dir = Vector3.Forward;
        }
        dir.Normalize();

        //find the yaw of the direction on the x/z plane
        //and use the sign of the x-component since we have 360 degrees
        //of freedom
        //_yaw = ;
        Yaw = (float)(Math.Acos(-dir.Z) * Math.Sign(dir.X));

        //Get the pitch from the angle formed by the Up vector and the 
        //the forward direction, then subtracting PI / 2, since 
        //we pitch is zero at Forward, not Up.
        //_pitch = ;
        Pitch = (float)-(Math.Acos(Vector3.Dot(Vector3.Up, dir)) - MathHelper.PiOver2);
    }
}