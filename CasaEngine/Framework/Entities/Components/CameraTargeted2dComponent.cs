using System.ComponentModel;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Camera Target 2d")]
public class CameraTargeted2dComponent : Camera3dComponent
{
    public static readonly int ComponentId = (int)ComponentIds.CameraTarget2d;

    private Vector3 _up = Vector3.Up;
    private Vector3 _target;
    Vector3 _offset;

    public Vector2 DeadZoneRatio { get; set; } = Vector2.One;
    public Rectangle Limits { get; set; }

    public override Vector3 Position
    {
        get
        {
            //const float w = _game.GraphicsDevice.Viewport.Width * Game::Instance().GetWindowSize().x;
            //const float h = _game.GraphicsDevice.Viewport.Height * Game::Instance().GetWindowSize().Y;

            float w = (float)_game.Window.ClientBounds.Width;
            float h = (float)_game.Window.ClientBounds.Height;

            var fov = FieldOfView * 0.5f;
            float z = -(h * 0.5f) / MathUtils.Tan(fov);
            return new(-_offset.X, -_offset.Y, z);
        }
    }

    public Vector3 Up
    {
        get { return _up; }
        set
        {
            _needToComputeViewMatrix = true;
            _up = value;
        }
    }

    public Entity? Target
    {
        get;
        set;
    }

    public CameraTargeted2dComponent(Entity entity) : base(entity, ComponentId)
    {

    }

    public override void Update(float elapsedTime)
    {
        _needToComputeViewMatrix = true;

        if (Target != null)
        {
            var viewport = _game.GraphicsDevice.Viewport;
            var winSize = _game.Window.ClientBounds.Size;
            var targetPosition = Target.Coordinates.Position;

            Rectangle deadZone = new Rectangle(
                (int)(_offset.X + winSize.X * (1.0f - DeadZoneRatio.X) / 2.0f),
                (int)(_offset.Y + winSize.Y * (1.0f - DeadZoneRatio.Y) / 2.0f),
                (int)(winSize.X * DeadZoneRatio.X),
                (int)(winSize.Y * DeadZoneRatio.Y));

            if (deadZone.Contains(targetPosition.X, targetPosition.Y))
            {
                if (targetPosition.X < deadZone.Left)
                {
                    _offset.X -= deadZone.Left - targetPosition.X;
                }
                else if (targetPosition.X > deadZone.Right)
                {
                    _offset.X += targetPosition.X - deadZone.Right;
                }

                if (targetPosition.Y < deadZone.Top)
                {
                    _offset.Y -= deadZone.Top - targetPosition.Y;
                }
                else if (targetPosition.Y > deadZone.Bottom)
                {
                    _offset.Y += targetPosition.Y - deadZone.Bottom;
                }

                //limits
                if (_offset.X > Limits.Right - winSize.X)
                {
                    _offset.X = Limits.Right - winSize.X;
                }

                if (_offset.X < Limits.Left)
                {
                    _offset.X = Limits.Left;
                }

                if (_offset.Y > Limits.Bottom - winSize.Y)
                {
                    _offset.Y = Limits.Bottom - winSize.Y;
                }

                if (_offset.Y < Limits.Top)
                {
                    _offset.Y = Limits.Top;
                }
            }
        }
    }

    protected override void ComputeViewMatrix()
    {
        _target = new(-_offset.X, -_offset.Y, 0.0f);
        _viewMatrix = Matrix.CreateLookAt(Position, _target, Up); //Vector3 up = -Vector3::UnitY();
    }

    public override Component Clone(Entity owner)
    {
        var component = new CameraTargeted2dComponent(owner);

        component._up = _up;
        component._target = _target;
        component._offset = _offset;

        return component;
    }
}