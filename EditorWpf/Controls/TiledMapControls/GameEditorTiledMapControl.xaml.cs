using System.Windows;
using System.Windows.Controls;

namespace EditorWpf.Controls.TiledMapControls;

public partial class GameEditorTiledMapControl : UserControl
{
    public GameEditorTiledMap GameEditor => gameEditor;

    public GameEditorTiledMapControl()
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