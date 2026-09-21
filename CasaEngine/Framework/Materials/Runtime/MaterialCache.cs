using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Materials.Runtime;

public sealed class MaterialCache
{
    private readonly Dictionary<Guid, CompiledMaterial> _compiledMaterials = new();
    private readonly Dictionary<Guid, MaterialBase> _runtimeMaterials = new();

    // ADR-0037: the textures and cubemaps a compiled entry reads are held through counted handles, one list
    // per material asset id, given back when that entry is replaced (Recompile/RecompileRuntimeMaterial),
    // dropped (Invalidate) or the cache is emptied (Clear).
    private readonly Dictionary<Guid, IReadOnlyList<IDisposable>> _textureHolds = new();
    private readonly MaterialCompiler _materialCompiler;

    public MaterialCache(MaterialCompiler materialCompiler = null)
    {
        _materialCompiler = materialCompiler ?? new MaterialCompiler();
    }

    public int Count => _compiledMaterials.Count;

    public bool TryGet(Guid materialAssetId, out CompiledMaterial compiledMaterial)
        => _compiledMaterials.TryGetValue(materialAssetId, out compiledMaterial!);

    public bool TryGetRuntimeMaterial(Guid materialAssetId, out MaterialBase runtimeMaterial)
        => _runtimeMaterials.TryGetValue(materialAssetId, out runtimeMaterial!);

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

        var compilation = _materialCompiler.CompileBoth(materialAsset, assetContentManager);
        ReleaseTextureHolds(materialAsset.Id);
        _compiledMaterials[materialAsset.Id] = compilation.CompiledMaterial;
        _runtimeMaterials[materialAsset.Id] = compilation.RuntimeMaterial;
        _textureHolds[materialAsset.Id] = compilation.TextureHolds;
        return compilation.CompiledMaterial;
    }

    public MaterialBase GetOrCompileRuntimeMaterial(MaterialAsset materialAsset, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (_runtimeMaterials.TryGetValue(materialAsset.Id, out var runtimeMaterial))
        {
            return runtimeMaterial;
        }

        return RecompileRuntimeMaterial(materialAsset, assetContentManager);
    }

    public MaterialBase RecompileRuntimeMaterial(MaterialAsset materialAsset, AssetContentManager assetContentManager)
    {
        ArgumentNullException.ThrowIfNull(materialAsset);
        ArgumentNullException.ThrowIfNull(assetContentManager);

        var compilation = _materialCompiler.CompileBoth(materialAsset, assetContentManager);
        ReleaseTextureHolds(materialAsset.Id);
        _compiledMaterials[materialAsset.Id] = compilation.CompiledMaterial;
        _runtimeMaterials[materialAsset.Id] = compilation.RuntimeMaterial;
        _textureHolds[materialAsset.Id] = compilation.TextureHolds;
        return compilation.RuntimeMaterial;
    }

    public bool Invalidate(Guid materialAssetId)
    {
        bool removedCompiled = _compiledMaterials.Remove(materialAssetId);
        bool removedRuntime = _runtimeMaterials.Remove(materialAssetId);
        ReleaseTextureHolds(materialAssetId);
        return removedCompiled || removedRuntime;
    }

    public void Clear()
    {
        foreach (var holds in _textureHolds.Values)
        {
            foreach (var hold in holds)
            {
                hold.Dispose();
            }
        }

        _textureHolds.Clear();
        _compiledMaterials.Clear();
        _runtimeMaterials.Clear();
    }

    private void ReleaseTextureHolds(Guid materialAssetId)
    {
        if (_textureHolds.Remove(materialAssetId, out var holds))
        {
            foreach (var hold in holds)
            {
                hold.Dispose();
            }
        }
    }
}