using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;

namespace EditorWpf.Controls.TiledMapControls;

public partial class TiledMapDetailsControl : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(TiledMapLayerDataViewModel), typeof(TiledMapDetailsControl));
    private GameEditorTiledMap _gameEditor;

    public TiledMapLayerDataViewModel? SelectedItem
    {
        get => (TiledMapLayerDataViewModel)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public TiledMapDetailsControl()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //SelectedItem = ListBox.SelectedItem as TiledMapLayerDataViewModel;
        SelectedItem = ListView.SelectedItem as TiledMapLayerDataViewModel;
    }

    public void InitializeFromGameEditor(GameEditorTiledMap gameEditor)
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

    public void LoadTiledMap(string fileName)
    {
        Clear();

        var tiledMapListModelView = DataContext as TiledMapDataViewModel;
        tiledMapListModelView.LoadMap(fileName);

        var assetContentManager = _gameEditor.Game.GameManager.AssetContentManager;
        var projectPath = GameSettings.ProjectSettings.ProjectPath;

        foreach (var spriteSheetFileName in tiledMapListModelView.TiledMapData.SpriteSheetFileNames)
        {
            SpriteLoader.LoadFromFile(Path.Combine(projectPath, spriteSheetFileName), assetContentManager, SaveOption.Editor);
        }

        foreach (var autoTileSetData in tiledMapListModelView.TiledMapData.AutoTileSetDatas)
        {
            SpriteLoader.LoadFromFile(Path.Combine(projectPath, autoTileSetData.SpriteSheetFileName), assetContentManager, SaveOption.Editor);
        }

        if (tiledMapListModelView.Layers.Count > 0)
        {
            Dispatcher.Invoke((Action)(() => ListView.SelectedIndex = 0));
        }

        _gameEditor.CreateMapEntities(tiledMapListModelView);
    }

    private void Clear()
    {
        var tiledMapLayersViewModel = DataContext as TiledMapDataViewModel;
        tiledMapLayersViewModel.Clear();
        _gameEditor.RemoveAllEntities();
    }
}