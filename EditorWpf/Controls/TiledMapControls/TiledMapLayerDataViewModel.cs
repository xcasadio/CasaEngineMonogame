using System;
using System.Collections.Generic;
using CasaEngine.Framework.Assets.Map2d;

namespace EditorWpf.Controls.TiledMapControls;

public class TiledMapLayerDataViewModel : NotifyPropertyChangeBase
{
    private bool _isVisible = true;
    public TiledMapLayerData TiledMapLayerData { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    public string? Name
    {
        get => TiledMapLayerData.Name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(TiledMapLayerData.Name, value)) return;
            TiledMapLayerData.Name = value;
            OnPropertyChanged();
        }
    }

    public List<int> Tiles
    {
        get => TiledMapLayerData.tiles;
        set
        {
            if (EqualityComparer<List<int>>.Default.Equals(TiledMapLayerData.tiles, value)) return;
            TiledMapLayerData.tiles = value;
            OnPropertyChanged();
        }
    }

    public TileType TileType
    {
        get => TiledMapLayerData.type;
        set
        {
            if (EqualityComparer<TileType>.Default.Equals(TiledMapLayerData.type, value)) return;
            TiledMapLayerData.type = value;
            OnPropertyChanged();
        }
    }

    public float ZOffset
    {
        get => TiledMapLayerData.zOffset;
        set
        {
            if (EqualityComparer<float>.Default.Equals(TiledMapLayerData.zOffset, value)) return;
            TiledMapLayerData.zOffset = value;
            OnPropertyChanged();
        }
    }

    public TiledMapLayerDataViewModel(TiledMapLayerData tiledMapLayerData)
    {
        TiledMapLayerData = tiledMapLayerData;
    }
}