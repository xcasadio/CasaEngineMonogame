﻿using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls.TileMapControls;

public class GameEditorTileMap : GameEditor2d
{
    private TileMapComponent _tileMapComponent;

    public GameEditorTileMap()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var tileMapDataViewModel = DataContext as TileMapDataViewModel;

        if (_tileMapComponent != null)
        {
            _tileMapComponent.TileMapData = tileMapDataViewModel.TileMapData;
            _tileMapComponent.Initialize(Game);
        }
    }

    protected override void CreateEntityComponents(Entity entity)
    {
        _tileMapComponent = new TileMapComponent(entity);
        entity.ComponentManager.Components.Add(_tileMapComponent);
    }

    public void CreateMapEntities(TileMapDataViewModel tileMapDataViewModel)
    {
        _tileMapComponent.TileMapData = (DataContext as TileMapDataViewModel).TileMapData;

        Game.GameManager.CurrentWorld.AddEntityImmediately(CameraEntity);
        Game.GameManager.CurrentWorld.AddEntityImmediately(_entity);
        _entity.Initialize(Game);
        //Game.GameManager.CurrentWorld.Initialize(Game);
    }

    public void RemoveAllEntities()
    {
        Game.GameManager.CurrentWorld.ClearEntities();
        Game.GameManager.AssetContentManager.Unload(AssetContentManager.DefaultCategory);
    }
}