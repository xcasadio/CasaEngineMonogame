using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class ComponentViewModel : NotifyPropertyChangeBase
{
    public ActorComponent Component { get; }

    public ComponentViewModel(ActorComponent component)
    {
        Component = component;
    }
}