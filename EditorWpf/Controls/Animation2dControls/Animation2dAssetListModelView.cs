using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Game;
using EditorWpf.Controls;

public class Animation2dAssetListModelView
{
    public ObservableCollection<AssetInfoViewModel> Animation2dAssetInfos { get; } = new();

    public Animation2dAssetListModelView()
    {
        GameSettings.AssetInfoManager.AssetAdded += OnAssetAdded;
        GameSettings.AssetInfoManager.AssetRemoved += OnAssetRemoved;
        GameSettings.AssetInfoManager.AssetCleared += OnAssetCleared;

        LoadAllAnimation2dAssetInfos();
    }

    private void LoadAllAnimation2dAssetInfos()
    {
        foreach (var assetInfo in GameSettings.AssetInfoManager.AssetInfos.Where(x => Path.GetExtension(x.FileName) == Constants.FileNameExtensions.Animation2d))
        {
            Animation2dAssetInfos.Add(new AssetInfoViewModel(assetInfo));
        }
    }

    private void OnAssetAdded(object? sender, AssetInfo assetInfo)
    {
        if (Path.GetExtension(assetInfo.FileName) == Constants.FileNameExtensions.Animation2d)
        {
            Animation2dAssetInfos.Add(new AssetInfoViewModel(assetInfo));
        }
    }

    private void OnAssetRemoved(object? sender, AssetInfo assetInfo)
    {
        var spriteDataViewModel = Animation2dAssetInfos.FirstOrDefault(x => x.Name == assetInfo.Name); // by id
        if (spriteDataViewModel != null)
        {
            Animation2dAssetInfos.Remove(spriteDataViewModel);
        }
    }

    private void OnAssetCleared(object? sender, EventArgs e)
    {
        Animation2dAssetInfos.Clear();
    }
}