using System.Windows.Input;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.TileMapControls;

public partial class TileMapEditorControl : EditorControlBase
{
    private string _tileMapFileName;

    protected override string LayoutFileName => "tileMapEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerTiledMap;

    public TileMapEditorControl()
    {
        InitializeComponent();
        DataContext = new TileMapDataViewModel();
        TiledMapDetailsControl.InitializeFromGameEditor(GameEditorControl.GameEditor);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Map Details" => TiledMapDetailsControl,
            "Tiled Map View" => GameEditorControl,
            "Tiles Definition" => TilesDefinitionControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenMap(string fileName)
    {
        _tileMapFileName = fileName;
        TiledMapDetailsControl.OpenMap(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tiledMapDataViewModel = TiledMapDetailsControl.DataContext as TileMapDataViewModel;
        TileMapLoader.Save(_tileMapFileName, tiledMapDataViewModel.TiledMapData);

        //tileset ??
        //autotileset ??

        e.Handled = true;
    }
}