using System.Windows.Controls;
using System.Windows.Input;

namespace CasaEngine.Editor.Controls.GuiEditorControls;

public partial class GameEditorGuiControl : UserControl
{
    public GameEditorGui GameEditor => gameEditor;

    public GameEditorGuiControl()
    {
        InitializeComponent();
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var screenViewModel = DataContext as ScreenViewModel;
        screenViewModel.Save();
    }
}