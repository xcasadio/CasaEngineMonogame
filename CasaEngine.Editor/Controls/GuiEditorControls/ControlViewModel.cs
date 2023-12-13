using TomShane.Neoforce.Controls;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public class ControlViewModel
{
    public string ControlName => Control.Tag as string;

    public Control Control { get; }

    public ControlViewModel(Control control)
    {
        Control = control;
    }
}