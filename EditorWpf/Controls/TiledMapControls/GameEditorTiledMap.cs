using System.Collections.Generic;
using CasaEngine.Framework.Assets.Map2d;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls.TiledMapControls;

public class GameEditorTiledMap : GameEditor2d
{
    public List<TileComponent> TileComponents { get; set; }

    public GameEditorTiledMap()
    {
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        var tiledMapDataViewModel = DataContext as TiledMapDataViewModel;


    }

    protected override void CreateEntityComponents(Entity entity)
    {
        //TileComponents = new TileComponent(entity);
        //entity.ComponentManager.Components.Add(TileComponents);
    }

    public void CreateMapEntities(TiledMapDataViewModel tiledMapDataViewModel)
    {
        TiledMapCreator.Create(_entity, "prefix",
            tiledMapDataViewModel.AutoTileSetData, tiledMapDataViewModel.TileSetData, tiledMapDataViewModel.TiledMapData,
            Game.GameManager.CurrentWorld, Game.GameManager.AssetContentManager);

        Game.GameManager.CurrentWorld.Initialize(Game);
    }
}