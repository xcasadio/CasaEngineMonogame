using System.Windows;
using System.Windows.Controls;

namespace CasaEngine.Editor.Controls.Animation2dControls;

public partial class GameEditorAnimation2dControl : UserControl
{
    public GameEditorAnimation2d GameEditor => gameEditor;

    public GameEditorAnimation2dControl()
    {
        InitializeComponent();
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (gameEditor == null)
        {
            return;
        }
        var value = ((e.AddedItems[0] as ComboBoxItem).Content as string).Remove(0, 1);
        gameEditor.Scale = float.Parse(value);
    }

    private void ButtonPlay_OnClick(object sender, RoutedEventArgs e)
    {

    }

    private void ButtonNextFrame_OnClick(object sender, RoutedEventArgs e)
    {

    }
}