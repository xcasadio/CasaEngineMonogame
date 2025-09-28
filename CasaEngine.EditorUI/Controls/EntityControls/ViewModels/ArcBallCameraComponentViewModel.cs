using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class ArcBallCameraComponentViewModel : SceneComponentViewModel
{
    private readonly ArcBallCameraComponent _arcBallCameraComponent;
    public ArcBallCameraComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _arcBallCameraComponent = (ArcBallCameraComponent)entityComponent;
    }

    public float Pitch
    {
        get => _arcBallCameraComponent.Pitch;
        set
        {
            if (_arcBallCameraComponent.Pitch != value)
            {
                _arcBallCameraComponent.Pitch = value;
                OnPropertyChanged();
            }
        }
    }

    public float Yaw
    {
        get => _arcBallCameraComponent.Yaw;
        set
        {
            if (_arcBallCameraComponent.Yaw != value)
            {
                _arcBallCameraComponent.Yaw = value;
                OnPropertyChanged();
            }
        }
    }

    public Vector3 Target
    {
        get => _arcBallCameraComponent.Target;
        set
        {
            if (_arcBallCameraComponent.Target != value)
            {
                _arcBallCameraComponent.Target = value;
                OnPropertyChanged();
            }
        }
    }

    public float Distance
    {
        get => _arcBallCameraComponent.Distance;
        set
        {
            if (_arcBallCameraComponent.Distance != value)
            {
                _arcBallCameraComponent.Distance = value;
                OnPropertyChanged();
            }
        }
    }

    public Vector3 Direction => _arcBallCameraComponent.Direction;
    public Vector3 Right => _arcBallCameraComponent.Right;

    public void OrbitUp(float angle)
    {
        _arcBallCameraComponent.OrbitUp(angle);
        OnPropertyChanged(nameof(Pitch));
    }

    public void OrbitRight(float angle)
    {
        _arcBallCameraComponent.OrbitRight(angle);
        OnPropertyChanged(nameof(Yaw));
    }

    public void RotateTargetUp(float angle)
    {
        _arcBallCameraComponent.RotateTargetUp(angle);
        OnPropertyChanged(nameof(Target));
    }

    public void RotateTargetRight(float angle)
    {
        _arcBallCameraComponent.RotateTargetRight(angle);
        OnPropertyChanged(nameof(Target));
    }
}