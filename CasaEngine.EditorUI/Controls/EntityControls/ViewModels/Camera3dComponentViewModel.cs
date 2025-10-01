using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class Camera3dComponentViewModel : CameraComponentViewModel
{
    private readonly Camera3dComponent _camera3dComponent;

    public Camera3dComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _camera3dComponent = (Camera3dComponent)entityComponent;
    }

    public float FieldOfView
    {
        get => _camera3dComponent.FieldOfView;
        set
        {
            if (_camera3dComponent.FieldOfView != value)
            {
                _camera3dComponent.FieldOfView = value;
                OnPropertyChanged();
            }
        }
    }
}