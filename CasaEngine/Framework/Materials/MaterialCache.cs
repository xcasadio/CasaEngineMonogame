using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Materials;

public sealed class MaterialCache
{
    private readonly Dictionary<Guid, CompiledMaterial> _compiledMaterials = new();
    private readonly MaterialCompiler _materialCompiler;

    public MaterialCache(MaterialCompiler? materialCompiler = null)
    {
        _materialCompiler = materialCompiler ?? new MaterialCompiler();
    }

    public int Count => _compiledMaterials.Count;

    public bool TryGet(Guid materialAssetId, out CompiledMaterial compiledMaterial)
        => _compiledMaterials.TryGetValue(materialAssetId, out compiledMaterial!);

    public CompiledMaterial GetOrCompile(MaterialAsset materialAsset, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (_compiledMaterials.TryGetValue(materialAsset.Id, out var compiledMaterial))
        {
            return compiledMaterial;
        }

        return Recompile(materialAsset, assetContentManager);
    }

    public CompiledMaterial Recompile(MaterialAsset materialAsset, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        var compiledMaterial = _materialCompiler.Compile(materialAsset, assetContentManager);
        _compiledMaterials[materialAsset.Id] = compiledMaterial;
        return compiledMaterial;
    }

    public bool Invalidate(Guid materialAssetId)
        => _compiledMaterials.Remove(materialAssetId);

    public void Clear()
    {
        _compiledMaterials.Clear();
    }
}