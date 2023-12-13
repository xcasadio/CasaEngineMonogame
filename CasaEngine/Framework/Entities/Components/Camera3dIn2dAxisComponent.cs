using System.ComponentModel;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Camera 3d in 2d axis")]
public class Camera3dIn2dAxisComponent : Camera3dComponent
{
    public override int ComponentId => (int)ComponentIds.Camera3dIn2dAxis;

    private Vector3 _up = Vector3.Up;
    private Vector3 _target;

    public override Vector3 Position
    {
        get
        {
            //const float w = _game.GraphicsDevice.Viewport.Width * Game::Instance().GetWindowSize().x;
            //const float h = _game.GraphicsDevice.Viewport.Height * Game::Instance().GetWindowSize().Y;

            float w = (float)Owner.Game.Window.ClientBounds.Width;
            float h = (float)Owner.Game.Window.ClientBounds.Height;

            var fov = FieldOfView * 0.5f;
            float z = (h * 0.5f) / MathUtils.Tan(fov);
            return new(_target.X, _target.Y, _target.Z + z);
        }
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

    public Vector3 Up
    {
        get => _up;
        set
        {
            _needToComputeViewMatrix = true;
            _up = value;
        }
    }

    public override void Update(float elapsedTime)
    {
        //Do nothing
    }

    protected override void ComputeViewMatrix()
    {
        _viewMatrix = Matrix.CreateLookAt(Position, _target, Up);
    }

    public override void ScreenResized(int width, int height)
    {
        base.ScreenResized(width, height);
        _needToComputeViewMatrix = true;
    }

    public override Component Clone()
    {
        return new Camera3dIn2dAxisComponent
        {
            _up = _up,
            _target = _target
        };
    }
}