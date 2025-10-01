using CasaEngine.Framework.Entities.Components;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class CameraComponentViewModel : SceneComponentViewModel
{
    private readonly CameraComponent _cameraComponent;

    public CameraComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _cameraComponent = (CameraComponent)entityComponent;
    }

    public float ViewDistance
    {
        get => _cameraComponent.ViewDistance;
    }

    public Viewport Viewport
    {
        get => _cameraComponent.Viewport;
    }
}