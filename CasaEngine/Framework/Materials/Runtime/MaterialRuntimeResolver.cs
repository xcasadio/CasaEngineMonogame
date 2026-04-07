using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;

namespace CasaEngine.Framework.Materials.Runtime;

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
            var materialCache = assetContentManager.RuntimeContext?.MaterialCache;
            if (materialCache != null && materialCache.TryGetRuntimeMaterial(materialAssetId, out material))
            {
                return true;
            }

            var authoringMaterialCache = assetContentManager.RuntimeContext?.MaterialAuthoringCache;
            var materialAsset = authoringMaterialCache != null
                ? authoringMaterialCache.GetOrLoad(materialAssetId, assetContentManager)
                : assetContentManager.Load<MaterialAsset>(materialAssetId, cache: false);
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