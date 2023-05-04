using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;

namespace EditorWpf.Controls.TiledMapControls;

public class TileListModelView
{
    private readonly AssetContentManager _assetContentManager;

    public ObservableCollection<TileComponent> TileComponents { get; } = new();

    public TileListModelView(GameEditorTiledMap gameEditor)
    {
        _assetContentManager = gameEditor.Game.GameManager.AssetContentManager;
    }

    public void LoadAnimations2d(string fileName)
    {
        //var tiles = TiledMapCreator.LoadMapFromFile(Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName), _assetContentManager);
        //
        //TileComponents.Clear();
        //foreach (var animation2dData in tiles)
        //{
        //    TileComponents.Add(new Animation2dDataViewModel(animation2dData));
        //}
    }
}