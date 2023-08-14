using Microsoft.Xna.Framework;
using System.ComponentModel;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("LookAt Camera")]
public class CameraLookAtComponent : Camera3dComponent
{
    public static readonly int ComponentId = (int)ComponentIds.LookAtCamera;
    private Vector3 _up = Vector3.Up;
    private Vector3 _target;
    private Vector3 _lastPosition;

    public override Vector3 Position
    {
        get { return Owner.Coordinates.Position; }
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

    public Vector3 Target
    {
        get { return _target; }
        set
        {
            _needToComputeViewMatrix = true;
            _target = value;
        }
    }

    public CameraLookAtComponent(Entity entity) : base(entity, ComponentId)
    {
        _lastPosition = entity.Coordinates.Position;
    }

    public override void Update(float elapsedTime)
    {
        if (_lastPosition != Position)
        {
            _needToComputeViewMatrix = true;
        }
    }

    protected override void ComputeViewMatrix()
    {
        _viewMatrix = Matrix.CreateLookAt(Position, Target, Up);
    }

    public override Component Clone(Entity owner)
    {
        var component = new CameraLookAtComponent(owner);

        component._up = _up;
        component._target = _target;
        component._lastPosition = _lastPosition;

        return component;
    }
}
