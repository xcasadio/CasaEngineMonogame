﻿using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets;

namespace EditorWpf.Controls.TileMapControls;

public partial class GameEditorTileMapControl : UserControl
{
    public GameEditorTileMap GameEditor => gameEditor;

    public GameEditorTileMapControl()
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

    private void ButtonSave_OnClick(object sender, RoutedEventArgs e)
    {
        var tileMapDataViewModel = DataContext as TileMapDataViewModel;

        if (tileMapDataViewModel?.TileMapData == null)
        {
            return;
        }

        AssetSaver.SaveAsset(tileMapDataViewModel.TileMapData.FileName, tileMapDataViewModel.TileMapData);
        LogManager.Instance.WriteLineInfo($"Entity {tileMapDataViewModel.TileMapData.Name} saved ({tileMapDataViewModel.TileMapData.FileName})");
    }
}