using System.Windows.Controls;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public partial class GameEditorGuiControl : UserControl
{
    public GameEditorGui GameEditor => gameEditor;

    public GameEditorGuiControl()
    {
        InitializeComponent();
    }
}