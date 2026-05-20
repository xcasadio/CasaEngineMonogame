namespace CasaEngine.Framework.Application;

public readonly record struct MaterialHotReloadMetrics(
    int AffectedMaterialCount,
    int InvalidatedRuntimeMaterialCount,
    int InvalidatedAuthoringMaterialCount,
    int RefreshedStaticModelComponentCount,
    int RecalculatedOverrideSlotCount,
    int AuthoringMaterialCacheHitCount,
    int AuthoringMaterialCacheMissCount,
    int InvalidatedViewCount,
    double ElapsedMilliseconds);

internal readonly record struct StaticModelHotReloadMetrics(
    int RefreshedStaticModelComponentCount,
    int RecalculatedOverrideSlotCount,
    int AuthoringMaterialCacheHitCount,
    int AuthoringMaterialCacheMissCount);

public readonly record struct ParticleHotReloadMetrics(
    int RefreshedParticleSystemComponentCount,
    int RebuiltRuntimeInstanceCount,
    int InvalidatedViewCount,
    double ElapsedMilliseconds);