using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;

namespace EditorWpf.Controls.TiledMapControls;

public class GameEditorTiledMap : GameEditor2d
{
    public GameEditorTiledMap()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        //var tiledMapDataViewModel = DataContext as TiledMapDataViewModel;
    }

    protected override void CreateEntityComponents(Entity entity)
    {
        //do nothing
    }

    public void CreateMapEntities(TiledMapDataViewModel tiledMapDataViewModel)
    {
        TiledMapLoader.Create(_entity, "prefix",
            tiledMapDataViewModel.AutoTileSetData, tiledMapDataViewModel.TileSetData, tiledMapDataViewModel.TiledMapData,
            Game.GameManager.CurrentWorld, Game.GameManager.AssetContentManager);

        Game.GameManager.CurrentWorld.Initialize(Game);
    }

    public void RemoveAllEntities()
    {
        Game.GameManager.CurrentWorld.Clear();
        Game.GameManager.AssetContentManager.Unload(AssetContentManager.DefaultCategory);
        Game.GameManager.CurrentWorld.AddEntityImmediately(_entity);
    }
}