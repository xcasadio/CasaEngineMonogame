using System.Windows.Input;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout.Serialization;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public partial class TileMapEditorControl : EditorControlBase
{
    private string _tileMapFileName;

    protected override string LayoutFileName => "tileMapEditorLayout.xml";
    public override DockingManager DockingManager => dockingManagerTileMap;

    public TileMapEditorControl()
    {
        InitializeComponent();
        DataContext = new TileMapDataViewModel();
        TileMapDetailsControl.InitializeFromGameEditor(GameEditorControl.GameEditor);
    }

    protected override void LayoutSerializationCallback(object? sender, LayoutSerializationCallbackEventArgs e)
    {
        e.Content = e.Model.Title switch
        {
            "Map Details" => TileMapDetailsControl,
            "TileMap View" => GameEditorControl,
            "Tiles Definition" => TilesDefinitionControl,
            "Logs" => this.FindParent<MainWindow>().LogsControl,
            "Content Browser" => this.FindParent<MainWindow>().ContentBrowserControl,
            _ => e.Content
        };
    }

    public void OpenMap(string fileName)
    {
        _tileMapFileName = fileName;
        TileMapDetailsControl.OpenMap(fileName);
    }

    private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tileMapDataViewModel = TileMapDetailsControl.DataContext as TileMapDataViewModel;
        TileMapLoader.Save(_tileMapFileName, tileMapDataViewModel.TileMapData);

        //tileset ??
        //autotileset ??

        e.Handled = true;
    }
}