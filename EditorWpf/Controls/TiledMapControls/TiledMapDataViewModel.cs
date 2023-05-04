using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Map2d;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.TiledMapControls;

public class TiledMapDataViewModel : NotifyPropertyChangeBase
{
    private readonly AssetContentManager _assetContentManager;

    public TiledMapData TiledMapData { get; private set; }
    public TileSetData TileSetData { get; private set; }
    public AutoTileSetData AutoTileSetData { get; private set; }

    public string TileSetFileName
    {
        get => TiledMapData.TileSetFileName;
        set
        {
            if (EqualityComparer<string>.Default.Equals(TiledMapData.TileSetFileName, value)) return;
            TiledMapData.TileSetFileName = value;
            OnPropertyChanged();
        }
    }

    public Vector2 MapSize
    {
        get => TiledMapData.MapSize;
        set
        {
            if (EqualityComparer<Vector2>.Default.Equals(TiledMapData.MapSize, value)) return;
            TiledMapData.MapSize = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TiledMapLayerDataViewModel> Layers { get; } = new();

    public TiledMapDataViewModel(AssetContentManager assetContentManager)
    {
        _assetContentManager = assetContentManager;
    }

    public void LoadMap(string fileName)
    {
        var (tiledMapData, tileSetData, autoTileSetData) = TiledMapCreator.LoadMapFromFile(fileName);

        TiledMapData = tiledMapData;
        TileSetData = tileSetData;
        AutoTileSetData = autoTileSetData;

        foreach (var layer in TiledMapData.Layers)
        {
            Layers.Add(new TiledMapLayerDataViewModel(layer));
        }
    }
}