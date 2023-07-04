using System.Windows.Input;
using CasaEngine.Framework.Assets.Map2d;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.TiledMapControls;

public partial class TiledMapEditorControl : EditorControlBase
{
    private string _tileMapFileName;

    protected override string LayoutFileName => "tiledMapEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerTiledMap;

    public TiledMapEditorControl()
    {
        InitializeComponent();
        DataContext = new TiledMapDataViewModel();
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

    public void LoadTiledMap(string fileName)
    {
        _tileMapFileName = fileName;
        TiledMapDetailsControl.LoadTiledMap(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tiledMapDataViewModel = TiledMapDetailsControl.DataContext as TiledMapDataViewModel;
        TiledMapLoader.Save(_tileMapFileName, tiledMapDataViewModel.TiledMapData);

        //tileset ??
        //autotileset ??

        e.Handled = true;
    }
}