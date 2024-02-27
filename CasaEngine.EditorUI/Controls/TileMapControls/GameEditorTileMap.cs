using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public class GameEditorTileMap : GameEditor2d
{
    private TileMapComponent _tileMapComponent;
    //private Entity _entity;

    public GameEditorTileMap()
    {
        //DataContextChanged += OnDataContextChanged;
    }

    protected override void InitializeGame()
    {

    }
    /*
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
    */
    protected override void CreateEntityComponents(Entity entity)
    {
        _tileMapComponent = new TileMapComponent();
        entity.RootComponent = _tileMapComponent;
    }

    public void CreateMapEntities(TileMapDataViewModel tileMapDataViewModel)
    {
        _tileMapComponent.TileMapData = tileMapDataViewModel.TileMapData;
    }
    /*
    public void RemoveAllEntities()
    {
        Game.GameManager.CurrentWorld.ClearEntities();
        Game.AssetContentManager.Unload(AssetContentManager.DefaultCategory);
    }*/
}