using System;
using System.Collections.ObjectModel;
using System.Linq;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using Path = System.IO.Path;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public class SpritesModelView
{
    public ObservableCollection<AssetInfoViewModel> SpriteAssetInfos { get; } = new();

    public SpritesModelView()
    {
        AssetCatalog.AssetAdded += OnAssetAdded;
        AssetCatalog.AssetRemoved += OnAssetRemoved;
        AssetCatalog.AssetCleared += OnAssetCleared;

        LoadAllSpriteAssetInfos();
    }

    private void LoadAllSpriteAssetInfos()
    {
        foreach (var assetInfo in AssetCatalog.AssetInfos.Where(x => Path.GetExtension(x.FileName) == Constants.FileNameExtensions.Sprite))
        {
            SpriteAssetInfos.Add(new AssetInfoViewModel(assetInfo.Id));
        }
    }

    private void OnAssetAdded(object? sender, AssetInfo assetInfo)
    {
        if (Path.GetExtension(assetInfo.FileName) == Constants.FileNameExtensions.Sprite)
        {
            SpriteAssetInfos.Add(new AssetInfoViewModel(assetInfo.Id));
        }
    }

    private void OnAssetRemoved(object? sender, AssetInfo assetInfo)
    {
        var spriteDataViewModel = SpriteAssetInfos.FirstOrDefault(x => x.Name == assetInfo.Name); // by id
        if (spriteDataViewModel != null)
        {
            SpriteAssetInfos.Remove(spriteDataViewModel);
        }
    }

    private void OnAssetCleared(object? sender, EventArgs e)
    {
        SpriteAssetInfos.Clear();
    }
}