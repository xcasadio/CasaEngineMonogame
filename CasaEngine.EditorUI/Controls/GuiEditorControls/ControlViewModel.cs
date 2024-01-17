using TomShane.Neoforce.Controls;

namespace CasaEngine.EditorUI.Controls.GuiEditorControls;

public class ControlViewModel
{
    public Control Control { get; }

    public ControlViewModel(Control control)
    {
        Control = control;
    }
}