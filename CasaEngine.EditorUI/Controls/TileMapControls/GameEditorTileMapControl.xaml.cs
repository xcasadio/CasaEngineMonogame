using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public partial class GameEditorTileMapControl : UserControl
{
    private ViewId _viewId = ViewId.Empty;
    private Entity? _previewEntity;
    private TileMapComponent? _tileMapComponent;
    private float _scale = 1.0f;

    public GameEditorTileMapControl()
    {
        InitializeComponent();

        if (EngineHost.Instance?.IsStarted == true)
        {
            OnEngineHostStarted(EngineHost.Instance, EventArgs.Empty);
        }
        else
        {
            EngineHost.InstanceStarted += OnEngineHostStarted;
        }
    }

    private void OnEngineHostStarted(object? sender, EventArgs e)
    {
        var host = EngineHost.Instance;
        if (host == null)
        {
            return;
        }

        _viewId = host.RegisterEditorView(new EditorViewDefinition
        {
            Name = "TileMap",
            ViewType = EditorViewType.TileMap,
            InitialWidth = Math.Max((int)gameViewport.ActualWidth, 1),
            InitialHeight = Math.Max((int)gameViewport.ActualHeight, 1),
        });

        gameViewport.Attach(host, _viewId);

        var ctx = host.GetViewContext(_viewId);
        var world = ctx?.World;
        if (world == null)
        {
            return;
        }

        _previewEntity = new Entity { Name = "TileMap Preview" };
        _tileMapComponent = new TileMapComponent();
        _previewEntity.RootComponent = _tileMapComponent;

        float cx = Math.Max((int)gameViewport.ActualWidth, 1) / 2f;
        float cy = Math.Max((int)gameViewport.ActualHeight, 1) / 2f;
        _previewEntity.RootComponent.Coordinates.Position = new Vector3(cx, cy, 0f);
        _previewEntity.RootComponent.Coordinates.Scale = new Vector3(_scale);

        _previewEntity.Initialize();
        _previewEntity.InitializeWithWorld(world);
        world.AddEntity(_previewEntity);
    }

    public void CreateMapEntities(TileMapDataViewModel tileMapDataViewModel)
    {
        if (_tileMapComponent != null)
        {
            _tileMapComponent.TileMapData = tileMapDataViewModel.TileMapData;
        }
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || _previewEntity?.RootComponent == null)
        {
            return;
        }

        var value = ((e.AddedItems[0] as ComboBoxItem)?.Content as string)?.Remove(0, 1);
        if (value == null)
        {
            return;
        }

        _scale = float.Parse(value);
        _previewEntity.RootComponent.Coordinates.Scale = new Vector3(_scale);
    }

    private void ButtonPlay_OnClick(object sender, RoutedEventArgs e)
    {
        // TODO
    }

    private void ButtonNextFrame_OnClick(object sender, RoutedEventArgs e)
    {
        // TODO
    }
}