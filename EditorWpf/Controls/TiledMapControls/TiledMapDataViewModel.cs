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

    public string? TileSetFileName
    {
        get => TiledMapData?.TileSetFileName;
        set
        {
            if (EqualityComparer<string>.Default.Equals(TiledMapData.TileSetFileName, value)) return;
            TiledMapData.TileSetFileName = value;
            OnPropertyChanged();
        }
    }

    public Size MapSize
    {
        get => TiledMapData?.MapSize ?? Size.Zero;
        set
        {
            if (EqualityComparer<Size>.Default.Equals(TiledMapData.MapSize, value)) return;
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

        OnPropertyChanged(nameof(MapSize));
        OnPropertyChanged(nameof(TileSetFileName));
    }

    public void Clear()
    {
        Layers.Clear();
    }
}