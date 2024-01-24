using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Core.Maths;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Game;

namespace CasaEngine.EditorUI.Controls.TileMapControls;

public class TileMapDataViewModel : NotifyPropertyChangeBase
{
    public TileMapData? TileMapData { get; private set; }
    public TileSetData? TileSetData { get; private set; }
    public AutoTileSetData? AutoTileSetData { get; private set; }

    public ObservableCollection<TileMapLayerDataViewModel> Layers { get; } = new();

    public Size MapSize
    {
        get => TileMapData?.MapSize ?? Size.Zero;
        set
        {
            if (EqualityComparer<Size>.Default.Equals(TileMapData.MapSize, value)) return;
            TileMapData.MapSize = value;
            OnPropertyChanged();
        }
    }

    public Size TileSize
    {
        get => TileSetData?.TileSize ?? Size.Zero;
        set
        {
            if (EqualityComparer<Size>.Default.Equals(TileSetData.TileSize, value)) return;
            TileSetData.TileSize = value;
            OnPropertyChanged();
        }
    }

    public void LoadMap(string fileName, AssetContentManager assetContentManager)
    {
        TileMapData = TileMapLoader.LoadMapFromFile(fileName);

        if (TileMapData.TileSetDataAssetId != Guid.Empty)
        {
            TileSetData = assetContentManager.Load<TileSetData>(TileMapData.TileSetDataAssetId);
        }

        foreach (var layer in TileMapData.Layers)
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