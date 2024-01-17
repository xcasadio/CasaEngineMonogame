using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class ComponentViewModel : NotifyPropertyChangeBase
{
    public Component Component { get; }

    public ComponentViewModel(Component component)
    {
        Component = component;
    }
}