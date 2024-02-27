using System.ComponentModel;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Camera 3d in 2d axis")]
public class Camera3dIn2dAxisComponent : Camera3dComponent
{
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

    public Camera3dIn2dAxisComponent()
    {
    }

    public Camera3dIn2dAxisComponent(Camera3dIn2dAxisComponent other) : base(other)
    {
        _target = other._target;
    }

    public override Camera3dIn2dAxisComponent Clone()
    {
        return new Camera3dIn2dAxisComponent(this);
    }

    protected override void ComputeViewMatrix()
    {
        var fov = FieldOfView * 0.5f;
        float z = (Owner.World.Game.ScreenSizeHeight * 0.5f) / MathUtils.Tan(fov);
        Position = new(_target.X, _target.Y, _target.Z + z);
        _viewMatrix = Matrix.CreateLookAt(Position, _target, Up);

        var direction = Target - Position;
        direction.Normalize();
        Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, Vector3.Dot(Vector3.UnitX, direction));
        IsBoundingBoxDirty = true;
    }

    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        Target = target;
    }

    public override void OnScreenResized(int width, int height)
    {
        base.OnScreenResized(width, height);
        _needToComputeViewMatrix = true;
    }
}