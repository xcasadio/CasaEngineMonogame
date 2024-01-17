using System.Collections.Generic;
using CasaEngine.Framework.Assets.TileMap;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public class TileMapLayerDataViewModel : NotifyPropertyChangeBase
{
    private bool _isVisible = true;
    public TileMapLayerData TileMapLayerData { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    public string? Name
    {
        get => TileMapLayerData.Name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(TileMapLayerData.Name, value)) return;
            TileMapLayerData.Name = value;
            OnPropertyChanged();
        }
    }

    public List<int> Tiles
    {
        get => TileMapLayerData.tiles;
        set
        {
            if (EqualityComparer<List<int>>.Default.Equals(TileMapLayerData.tiles, value)) return;
            TileMapLayerData.tiles = value;
            OnPropertyChanged();
        }
    }

    public float ZOffset
    {
        get => TileMapLayerData.zOffset;
        set
        {
            if (EqualityComparer<float>.Default.Equals(TileMapLayerData.zOffset, value)) return;
            TileMapLayerData.zOffset = value;
            OnPropertyChanged();
        }
    }

    public TileMapLayerDataViewModel(TileMapLayerData tileMapLayerData)
    {
        TileMapLayerData = tileMapLayerData;
    }
}