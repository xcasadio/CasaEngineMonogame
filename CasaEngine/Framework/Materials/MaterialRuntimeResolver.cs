using CasaEngine.Core.Log;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Materials;

public static class MaterialRuntimeResolver
{
    private static readonly MaterialCompiler MaterialCompiler = new();

    public static bool TryLoadRuntimeMaterial(Guid materialAssetId, AssetContentManager assetContentManager, out MaterialBase material)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        material = null!;
        if (materialAssetId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var materialAsset = assetContentManager.Load<MaterialAsset>(materialAssetId, cache: false);
            var materialCache = assetContentManager.RuntimeContext?.MaterialCache;
            material = materialCache != null
                ? materialCache.GetOrCompileRuntimeMaterial(materialAsset, assetContentManager)
                : MaterialCompiler.CompileRuntimeMaterial(materialAsset, assetContentManager);
            return true;
        }
        catch (Exception exception)
        {
            Logs.WriteException(exception);
            return false;
        }
    }
}