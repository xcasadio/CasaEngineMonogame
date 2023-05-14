using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Core;
using CasaEngine.Framework.Assets.Map2d;

namespace EditorWpf.Controls.TiledMapControls;

public class TiledMapDataViewModel : NotifyPropertyChangeBase
{
    public TiledMapData? TiledMapData { get; private set; }
    public AutoTileSetData? AutoTileSetData { get; private set; }

    public ObservableCollection<TiledMapLayerDataViewModel> Layers { get; } = new();

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

    public Size TileSize
    {
        get => TiledMapData?.TileSize ?? Size.Zero;
        set
        {
            if (EqualityComparer<Size>.Default.Equals(TiledMapData.TileSize, value)) return;
            TiledMapData.TileSize = value;
            OnPropertyChanged();
        }
    }

    public void LoadMap(string fileName)
    {
        var tiledMapData = TiledMapLoader.LoadMapFromFile(fileName);

        TiledMapData = tiledMapData;

        foreach (var layer in TiledMapData.Layers)
        {
            Layers.Add(new TiledMapLayerDataViewModel(layer));
        }

        OnPropertyChanged(nameof(MapSize));
        OnPropertyChanged(nameof(TileSize));
    }

    public void Clear()
    {
        Layers.Clear();
    }
}