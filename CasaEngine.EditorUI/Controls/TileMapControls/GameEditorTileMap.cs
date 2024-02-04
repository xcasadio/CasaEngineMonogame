using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
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

    protected override void InitializeGame()
    {

    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var tileMapDataViewModel = DataContext as TileMapDataViewModel;

        if (_tileMapComponent != null)
        {
            _tileMapComponent.TileMapData = tileMapDataViewModel.TileMapData;
            _tileMapComponent.Initialize();
            _tileMapComponent.InitializeWithWorld(Game.GameManager.CurrentWorld);
        }
    }

    protected override void CreateEntityComponents(AActor entity)
    {
        _entity = entity;
        _tileMapComponent = new TileMapComponent();
        entity.RootComponent = _tileMapComponent;
    }

    public void CreateMapEntities(TileMapDataViewModel tileMapDataViewModel)
    {
        _tileMapComponent.TileMapData = tileMapDataViewModel.TileMapData;

        Game.GameManager.CurrentWorld.AddEntityWithEditor(_entity);
        //_entity.Initialize();
        //_entity.InitializeWithWorld(Game.GameManager.CurrentWorld);
    }

    public void RemoveAllEntities()
    {
        Game.GameManager.CurrentWorld.ClearEntities();
        Game.AssetContentManager.Unload(AssetContentManager.DefaultCategory);
    }
}