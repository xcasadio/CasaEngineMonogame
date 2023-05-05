using System.Windows.Input;
using CasaEngine.Framework.Assets.Map2d;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace EditorWpf.Controls.TiledMapControls;

public partial class TiledMapEditorControl : EditorControlBase
{
    private string _tileMapFileName;

    protected override string LayoutFileName { get; } = "tiledMapEditorLayout.xml";
    protected override DockingManager DockingManager => dockingManagerTiledMap;

    public TiledMapEditorControl()
    {
        InitializeComponent();
        DataContext = new TiledMapDataViewModel();
        TiledMapLayersControl.InitializeFromGameEditor(GameEditorControl.GameEditor);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Layers" => TiledMapLayersControl,
            "Tiled Map View" => GameEditorControl,
            "Details" => TiledMapDetailsControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void LoadTiledMap(string fileName)
    {
        _tileMapFileName = fileName;
        TiledMapLayersControl.LoadTiledMap(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tiledMapDataViewModel = TiledMapLayersControl.DataContext as TiledMapDataViewModel;
        TiledMapLoader.Save(_tileMapFileName, tiledMapDataViewModel.TiledMapData);

        //tileset ??
        //autotileset ??

        e.Handled = true;
    }
}