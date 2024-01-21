using System.ComponentModel;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("Camera 3d in 2d axis")]
public class Camera3dIn2dAxisComponent : Camera3dComponent
{
    private Vector3 _up = Vector3.Up;
    private Vector3 _target;

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

    public Camera3dIn2dAxisComponent()
    {

    }

    public Camera3dIn2dAxisComponent(Camera3dIn2dAxisComponent other) : base(other)
    {
        _up = other._up;
        _target = other._target;
    }

    public override Camera3dIn2dAxisComponent Clone()
    {
        return new Camera3dIn2dAxisComponent(this);
    }

    public override void Update(float elapsedTime)
    {
        //Do nothing
    }

    protected override void ComputeViewMatrix()
    {
        var fov = FieldOfView * 0.5f;
        float z = (World.Game.ScreenSizeHeight * 0.5f) / MathUtils.Tan(fov);
        Position = new(_target.X, _target.Y, _target.Z + z);
        _viewMatrix = Matrix.CreateLookAt(Position, _target, Up);
    }

    public override void ScreenResized(int width, int height)
    {
        base.ScreenResized(width, height);
        _needToComputeViewMatrix = true;
    }
}