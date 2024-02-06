using System.ComponentModel;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement.Components;

[DisplayName("LookAt Camera")]
public class CameraLookAtComponent : Camera3dComponent
{
    private Vector3 _target;
    private Vector3 _lastPosition;

    public Vector3 Target
    {
        get => _target;
        set
        {
            _needToComputeViewMatrix = true;
            _target = value;
        }
    }

    public CameraLookAtComponent()
    {
    }

    public CameraLookAtComponent(CameraLookAtComponent other) : base(other)
    {
        other._target = _target;
        other._lastPosition = _lastPosition;
    }

    public override CameraLookAtComponent Clone()
    {
        return new CameraLookAtComponent(this);
    }

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _lastPosition = Position;
    }

    public override void Update(float elapsedTime)
    {
        if (_lastPosition != Position)
        {
            _needToComputeViewMatrix = true;
        }

        base.Update(elapsedTime);
    }

    protected override void ComputeViewMatrix()
    {
        _viewMatrix = Matrix.CreateLookAt(Position, Target, Up);
    }

    public override void SetPositionAndTarget(Vector3 position, Vector3 target)
    {
        Position = position;
        Target = target;
    }
}
