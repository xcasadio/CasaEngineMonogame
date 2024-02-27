using System.ComponentModel;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Camera Target 2d")]
public class CameraTargeted2dComponent : Camera3dComponent
{
    private Vector3 _target;
    Vector3 _offset;

    public Vector2 DeadZoneRatio { get; set; } = Vector2.One;
    public Rectangle Limits { get; set; }

    public Entity? Target { get; set; }

    public CameraTargeted2dComponent()
    {

    }

    public CameraTargeted2dComponent(CameraTargeted2dComponent other) : base(other)
    {
        Target = other.Target;
        _offset = other._offset;
    }

    public override CameraTargeted2dComponent Clone()
    {
        return new CameraTargeted2dComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        _needToComputeViewMatrix = true;

        if (Target != null)
        {
            var viewport = Owner.World.Game.GraphicsDevice.Viewport;
            var screenWidth = Owner.World.Game.ScreenSizeWidth;
            var screenHeight = Owner.World.Game.ScreenSizeHeight;
            var targetPosition = Target?.RootComponent?.Coordinates.Position ?? Vector3.Zero;

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

        base.Update(elapsedTime);
    }

    protected override void ComputeViewMatrix()
    {

        var fov = FieldOfView * 0.5f;
        float z = -((float)Owner.World.Game.ScreenSizeHeight * 0.5f) / MathUtils.Tan(fov);
        Position = new(-_offset.X, -_offset.Y, z);

        _target = new(-_offset.X, -_offset.Y, 0.0f);
        _viewMatrix = Matrix.CreateLookAt(Position, _target, Up); //Vector3 up = -Vector3::UnitY();
    }

    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        //Do nothing
    }
}