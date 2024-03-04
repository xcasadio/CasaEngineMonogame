using CasaEngine.Framework.GUI.Neoforce;

namespace CasaEngine.EditorUI.Controls.GuiEditorControls;

public class ControlViewModel
{
    public Control Control { get; }

    public ControlViewModel(Control control)
    {
        Control = control;
    }
}