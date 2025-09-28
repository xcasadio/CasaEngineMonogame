using CasaEngine.Framework.Entities.Components;
using System;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class TileMapComponentViewModel : SceneComponentViewModel
{
    private readonly TileMapComponent _tileMapComponent;
    public TileMapComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _tileMapComponent = (TileMapComponent)entityComponent;
    }

    public Guid TileMapDataAssetId
    {
        get => _tileMapComponent.TileMapDataAssetId;
        set
        {
            if (_tileMapComponent.TileMapDataAssetId != value)
            {
                _tileMapComponent.TileMapDataAssetId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasTileMap));
            }
        }
    }

    public bool HasTileMap => _tileMapComponent.TileMapDataAssetId != Guid.Empty;
}