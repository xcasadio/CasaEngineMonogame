using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public class GameEditorTileMap : GameEditor2d
{
    private TileMapComponent _tileMapComponent;
    private AActor _entity;

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
            _tileMapComponent.Initialize(_entity);
        }
    }

    protected override void CreateEntityComponents(AActor entity)
    {
        _entity = entity;
        _tileMapComponent = new TileMapComponent();
        entity.ComponentManager.Add(_tileMapComponent);
    }

    public void CreateMapEntities(TileMapDataViewModel tileMapDataViewModel)
    {
        _tileMapComponent.TileMapData = tileMapDataViewModel.TileMapData; //(DataContext as TileMapDataViewModel).TileMapData;

        Game.GameManager.CurrentWorld.AddEntity(CameraEntity);
        Game.GameManager.CurrentWorld.AddEntity(_entity);
        _entity.Initialize(Game);
        //Game.GameManager.CurrentWorld.Initialize(Game);
    }

    public void RemoveAllEntities()
    {
        Game.GameManager.CurrentWorld.ClearEntities();
        Game.GameManager.AssetContentManager.Unload(AssetContentManager.DefaultCategory);
    }
}