using CasaEngine.Core.Maths;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class SceneComponentViewModel : ComponentViewModel
{
    private readonly SceneComponent _sceneComponent;

    public Coordinates Coordinates => _sceneComponent.Coordinates;

    protected SceneComponentViewModel(EntityComponent component) : base(component)
    {
        _sceneComponent = (SceneComponent)component;
    }
}