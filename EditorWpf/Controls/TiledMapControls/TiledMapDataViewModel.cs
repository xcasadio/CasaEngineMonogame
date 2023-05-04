using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets.Map2d;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.TiledMapControls;

public class TiledMapDataViewModel : NotifyPropertyChangeBase
{
    public TiledMapData TiledMapData { get; }

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

    public TiledMapDataViewModel(TiledMapData TiledMapData)
    {
        TiledMapData = TiledMapData;

        foreach (var layer in TiledMapData.Layers)
        {
            Layers.Add(new TiledMapLayerDataViewModel(layer));
        }
    }
}