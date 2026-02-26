using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.EditorUI.Controls;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public partial class TileMapDetailsControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(TileMapLayerDataViewModel), typeof(TileMapDetailsControl));

    public TileMapLayerDataViewModel? SelectedItem
    {
        get => (TileMapLayerDataViewModel)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public TileMapDetailsControl()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //SelectedItem = ListBox.SelectedItem as TiledMapLayerDataViewModel;
        SelectedItem = ListView.SelectedItem as TileMapLayerDataViewModel;
    }

    /// <summary>Shared-game overload.</summary>
    public void InitializeFromEngineHost(EngineHost host)
    {
        // Nothing to do on start for TileMapDetails — content is set via DataContext.
    }

    private void ListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView listBox && listBox.SelectedItem != null)
        {
            //var inputTextBox = new InputTextBox();
            //inputTextBox.Description = "Enter a new name";
            //inputTextBox.Title = "Rename";
            //var TiledMapDataViewModel = (listBox.SelectedItem as TiledMapDataViewModel);
            //inputTextBox.Text = TiledMapDataViewModel.Name;
            //
            //if (inputTextBox.ShowDialog() == true)
            //{
            //    _gameEditor.Game.AssetContentManager.Rename(TiledMapDataViewModel.Name, inputTextBox.Text);
            //    TiledMapDataViewModel.Name = inputTextBox.Text;
            //}
        }
    }

    public void OpenMap(string fileName)
    {
        Clear();

        var tileMapDataViewModel = DataContext as TileMapDataViewModel;
        tileMapDataViewModel.LoadMap(fileName, EngineHost.Instance!.Game.AssetContentManager);

        //var assetContentManager = EngineHost.Instance?.Game?.AssetContentManager;
        //var projectPath = EngineEnvironment.ProjectPath;

        //foreach (var spriteSheetFileName in tileMapDataViewModel.TiledMapData.SpriteSheetFileNames)
        //{
        //    SpriteLoader.LoadFromFile(Path.Combine(projectPath, spriteSheetFileName), assetContentManager, SaveOption.Editor);
        //}
        //
        //foreach (var autoTileSetData in tileMapDataViewModel.TiledMapData.AutoTileSetDatas)
        //{
        //    SpriteLoader.LoadFromFile(Path.Combine(projectPath, autoTileSetData.SpriteSheetFileName), assetContentManager, SaveOption.Editor);
        //}

        if (tileMapDataViewModel.Layers.Count > 0)
        {
            Dispatcher.Invoke((Action)(() => ListView.SelectedIndex = 0));
        }
    }

    private void Clear()
    {
        var tileMapDataViewModel = DataContext as TileMapDataViewModel;
        tileMapDataViewModel.Clear();
        //_gameEditor.RemoveAllEntities();
    }
}