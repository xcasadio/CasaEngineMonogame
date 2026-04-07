using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Materials.Authoring;

internal sealed class MaterialAuthoringAssetCache
{
    private readonly Dictionary<Guid, MaterialAsset> _materialAssets = new();

    public int Count => _materialAssets.Count;

    public bool TryGet(Guid materialAssetId, out MaterialAsset materialAsset)
        => _materialAssets.TryGetValue(materialAssetId, out materialAsset!);

    public void Set(MaterialAsset materialAsset)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        _materialAssets[materialAsset.Id] = materialAsset;
    }

    public MaterialAsset GetOrLoad(Guid materialAssetId, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (_materialAssets.TryGetValue(materialAssetId, out var materialAsset))
        {
            return materialAsset;
        }

        materialAsset = assetContentManager.Load<MaterialAsset>(materialAssetId, cache: false);
        _materialAssets[materialAssetId] = materialAsset;
        return materialAsset;
    }

    public bool Invalidate(Guid materialAssetId)
        => _materialAssets.Remove(materialAssetId);

    public void Clear()
        => _materialAssets.Clear();
}