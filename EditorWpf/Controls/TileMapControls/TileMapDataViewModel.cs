using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Core.Maths;
using CasaEngine.Framework.Assets.TileMap;

namespace EditorWpf.Controls.TileMapControls;

public class TileMapDataViewModel : NotifyPropertyChangeBase
{
    public TileMapData? TiledMapData { get; private set; }
    public AutoTileSetData? AutoTileSetData { get; private set; }

    public ObservableCollection<TileMapLayerDataViewModel> Layers { get; } = new();

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
        get => TiledMapData?.TileSetData.TileSize ?? Size.Zero;
        set
        {
            if (EqualityComparer<Size>.Default.Equals(TiledMapData.TileSetData.TileSize, value)) return;
            TiledMapData.TileSetData.TileSize = value;
            OnPropertyChanged();
        }
    }

    public void LoadMap(string fileName)
    {
        var tiledMapData = TileMapLoader.LoadMapFromFile(fileName);

        TiledMapData = tiledMapData;

        foreach (var layer in TiledMapData.Layers)
        {
            Layers.Add(new TileMapLayerDataViewModel(layer));
        }

        OnPropertyChanged(nameof(MapSize));
        OnPropertyChanged(nameof(TileSize));
    }

    public void Clear()
    {
        Layers.Clear();
    }
}