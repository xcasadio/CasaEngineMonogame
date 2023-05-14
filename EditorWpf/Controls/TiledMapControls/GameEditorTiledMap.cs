using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls.TiledMapControls;

public class GameEditorTiledMap : GameEditor2d
{
    private TiledMapComponent _tiledMapComponent;

    public GameEditorTiledMap()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var tiledMapDataViewModel = DataContext as TiledMapDataViewModel;

        if (_tiledMapComponent != null)
        {
            _tiledMapComponent.TiledMapData = tiledMapDataViewModel.TiledMapData;
            _tiledMapComponent.Initialize(Game);
        }
    }

    protected override void CreateEntityComponents(Entity entity)
    {
        _tiledMapComponent = new TiledMapComponent(entity);
        entity.ComponentManager.Components.Add(_tiledMapComponent);
    }

    public void CreateMapEntities(TiledMapDataViewModel tiledMapDataViewModel)
    {
        TiledMapLoader.Create(tiledMapDataViewModel.TiledMapData, Game.GameManager.AssetContentManager);
        _tiledMapComponent.TiledMapData = (DataContext as TiledMapDataViewModel).TiledMapData;
        Game.GameManager.CurrentWorld.Initialize(Game);
    }

    public void RemoveAllEntities()
    {
        Game.GameManager.CurrentWorld.Clear();
        Game.GameManager.AssetContentManager.Unload(AssetContentManager.DefaultCategory);
        Game.GameManager.CurrentWorld.AddEntityImmediately(_entity);
    }
}