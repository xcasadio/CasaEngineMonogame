using System;
using System.Collections.ObjectModel;
using System.Linq;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using Path = System.IO.Path;

namespace CasaEngine.EditorUI.Controls.SpriteControls;

public class SpritesModelView
{
    public ObservableCollection<AssetInfoViewModel> SpriteAssetInfos { get; } = new();

    public SpritesModelView()
    {
        GameSettings.AssetInfoManager.AssetAdded += OnAssetAdded;
        GameSettings.AssetInfoManager.AssetRemoved += OnAssetRemoved;
        GameSettings.AssetInfoManager.AssetCleared += OnAssetCleared;

        LoadAllSpriteAssetInfos();
    }

    private void LoadAllSpriteAssetInfos()
    {
        foreach (var assetInfo in GameSettings.AssetInfoManager.AssetInfos.Where(x => Path.GetExtension(x.FileName) == Constants.FileNameExtensions.Sprite))
        {
            SpriteAssetInfos.Add(new AssetInfoViewModel(assetInfo));
        }
    }

    private void OnAssetAdded(object? sender, AssetInfo assetInfo)
    {
        if (Path.GetExtension(assetInfo.FileName) == Constants.FileNameExtensions.Sprite)
        {
            SpriteAssetInfos.Add(new AssetInfoViewModel(assetInfo));
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