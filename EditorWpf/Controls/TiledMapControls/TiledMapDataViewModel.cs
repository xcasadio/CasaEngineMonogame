using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Core;
using CasaEngine.Framework.Assets.Map2d;

namespace EditorWpf.Controls.TiledMapControls;

public class TiledMapDataViewModel : NotifyPropertyChangeBase
{
    public TiledMapData? TiledMapData { get; private set; }
    public TileSetData? TileSetData { get; private set; }
    public AutoTileSetData? AutoTileSetData { get; private set; }

    public ObservableCollection<TiledMapLayerDataViewModel> Layers { get; } = new();

    public string TileSetFileName
    {
        get => TiledMapData?.TileSetFileName;
        set
        {
            if (EqualityComparer<string>.Default.Equals(TiledMapData.TileSetFileName, value)) return;
            TiledMapData.TileSetFileName = value;
            OnPropertyChanged();
        }
    }

    public CasaEngine.Core.Size MapSize
    {
        get => TiledMapData == null ? Size.Zero : TiledMapData.MapSize;
        set
        {
            if (EqualityComparer<CasaEngine.Core.Size>.Default.Equals(TiledMapData.MapSize, value)) return;
            TiledMapData.MapSize = value;
            OnPropertyChanged();
        }
    }

    public void LoadMap(string fileName)
    {
        var (tiledMapData, tileSetData, autoTileSetData) = TiledMapLoader.LoadMapFromFile(fileName);

        TiledMapData = tiledMapData;
        TileSetData = tileSetData;
        AutoTileSetData = autoTileSetData;

        foreach (var layer in TiledMapData.Layers)
        {
            Layers.Add(new TiledMapLayerDataViewModel(layer));
        }
    }

    public void Clear()
    {
        Layers.Clear();
    }
}