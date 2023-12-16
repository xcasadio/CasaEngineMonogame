using System.ComponentModel;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Camera Target 2d")]
public class CameraTargeted2dComponent : Camera3dComponent
{
    public override int ComponentId => (int)ComponentIds.CameraTarget2d;

    private Vector3 _up = Vector3.Up;
    private Vector3 _target;
    Vector3 _offset;

    public Vector2 DeadZoneRatio { get; set; } = Vector2.One;
    public Rectangle Limits { get; set; }

    public override Vector3 Position
    {
        get
        {
            var fov = FieldOfView * 0.5f;
            float z = -((float)Owner.Game.ScreenSizeHeight * 0.5f) / MathUtils.Tan(fov);
            return new(-_offset.X, -_offset.Y, z);
        }
    }

    public Vector3 Up
    {
        get => _up;
        set
        {
            _needToComputeViewMatrix = true;
            _up = value;
        }
    }

    public Entity? Target { get; set; }

    public override void Update(float elapsedTime)
    {
        _needToComputeViewMatrix = true;

        if (Target != null)
        {
            var viewport = Owner.Game.GraphicsDevice.Viewport;
            var screenWidth = Owner.Game.ScreenSizeWidth;
            var screenHeight = Owner.Game.ScreenSizeHeight;
            var targetPosition = Target.Coordinates.Position;

            Rectangle deadZone = new Rectangle(
                (int)(_offset.X + screenWidth * (1.0f - DeadZoneRatio.X) / 2.0f),
                (int)(_offset.Y + screenHeight * (1.0f - DeadZoneRatio.Y) / 2.0f),
                (int)(screenWidth * DeadZoneRatio.X),
                (int)(screenHeight * DeadZoneRatio.Y));

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
                if (_offset.X > Limits.Right - screenWidth)
                {
                    _offset.X = Limits.Right - screenWidth;
                }

                if (_offset.X < Limits.Left)
                {
                    _offset.X = Limits.Left;
                }

                if (_offset.Y > Limits.Bottom - screenHeight)
                {
                    _offset.Y = Limits.Bottom - screenHeight;
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

    public override Component Clone()
    {
        var component = new CameraTargeted2dComponent
        {
            Up = _up,
            Target = Target,
            _offset = _offset
        };

        return component;
    }
}