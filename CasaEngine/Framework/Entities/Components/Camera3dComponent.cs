using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Entities.Components;

public abstract class Camera3dComponent : CameraComponent
{
    private float _fieldOfView;

    protected Camera3dComponent(Entity entity, int type) : base(entity, type)
    {
        _fieldOfView = MathHelper.PiOver4;
    }

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            _fieldOfView = value;
            _needToComputeProjectionMatrix = true;
        }
    }

    protected override void ComputeProjectionMatrix()
    {
        _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            _fieldOfView,
            _viewport.AspectRatio,
            _viewport.MinDepth,
            _viewport.MaxDepth);

        _needToComputeProjectionMatrix = false;
    }

}