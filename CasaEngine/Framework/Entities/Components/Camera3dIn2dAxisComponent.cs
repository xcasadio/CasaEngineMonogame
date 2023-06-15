using System.ComponentModel;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Camera 3d in 2d axis")]
public class Camera3dIn2dAxisComponent : Camera3dComponent
{
    public static readonly int ComponentId = (int)ComponentIds.Camera3dIn2dAxis;

    private Vector3 _up = -Vector3.Up;
    private Vector3 _target;


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

    public Camera3dIn2dAxisComponent(Entity entity) : base(entity, ComponentId)
    {

    }

    public override void Update(float elapsedTime)
    {
        //Do nothing
    }

    protected override void ComputeViewMatrix()
    {
        _viewMatrix = Matrix.CreateLookAt(Position, _target, Up); //Vector3 up = -Vector3::UnitY();
    }
}