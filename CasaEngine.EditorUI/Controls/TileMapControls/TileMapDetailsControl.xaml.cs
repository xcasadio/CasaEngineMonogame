using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;

namespace CasaEngine.Editor.Controls.TileMapControls;

public partial class TileMapDetailsControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(TileMapLayerDataViewModel), typeof(TileMapDetailsControl));
    private GameEditorTileMap _gameEditor;

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

    public void InitializeFromGameEditor(GameEditorTileMap gameEditor)
    {
        _gameEditor = gameEditor;
        _gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        //Do nothing
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
            //    _gameEditor.Game.GameManager.AssetContentManager.Rename(TiledMapDataViewModel.Name, inputTextBox.Text);
            //    TiledMapDataViewModel.Name = inputTextBox.Text;
            //}
        }
    }

    public void OpenMap(string fileName)
    {
        Clear();

        var tileMapDataViewModel = DataContext as TileMapDataViewModel;
        tileMapDataViewModel.LoadMap(fileName, _gameEditor.Game.GameManager.AssetContentManager);

        //var assetContentManager = _gameEditor.Game.GameManager.AssetContentManager;
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

        _gameEditor.CreateMapEntities(tileMapDataViewModel);
    }

    private void Clear()
    {
        var tileMapDataViewModel = DataContext as TileMapDataViewModel;
        tileMapDataViewModel.Clear();
        _gameEditor.RemoveAllEntities();
    }
}