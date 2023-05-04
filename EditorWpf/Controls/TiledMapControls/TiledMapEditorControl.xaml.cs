using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.TiledMapControls;

public partial class TiledMapEditorControl : UserControl, IEditorControl
{
    private string _tileMapFileName;
    private const string LayoutFileName = "tiledMapEditorLayout.xml";

    public TiledMapEditorControl()
    {
        InitializeComponent();
        TiledMapListControl.InitializeFromGameEditor(GameEditorControl.GameEditor);
    }
    public void ShowControl(UserControl control, string panelTitle)
    {
        EditorControlHelper.ShowControl(control, dockingManagerTiledMap, panelTitle);
    }

    public void LoadLayout(string path)
    {
        var fileName = Path.Combine(path, LayoutFileName);

        if (!File.Exists(fileName))
        {
            return;
        }

        EditorControlHelper.LoadLayout(dockingManagerTiledMap, fileName, LayoutSerializationCallback);
    }

    private void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Tiled Map" => TiledMapListControl,
            "Tiled Map View" => GameEditorControl,
            "Details" => TiledMapDetailsControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void SaveLayout(string path)
    {
        var fileName = Path.Combine(path, LayoutFileName);
        EditorControlHelper.SaveLayout(dockingManagerTiledMap, fileName);
    }

    public void LoadTiledMap(string fileName)
    {
        _tileMapFileName = fileName;
        TiledMapListControl.LoadTiledMap(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        //var animation2dDatas = GameEditorTiledMapControl.GameEditor.Game.GameManager.AssetContentManager.GetAssets<TiledMapData>();
        //Animation2dLoader.SaveToFile(_animation2dFileName, animation2dDatas);
        //e.Handled = true;
    }
}