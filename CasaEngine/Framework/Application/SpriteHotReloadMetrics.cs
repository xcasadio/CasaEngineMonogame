namespace CasaEngine.Framework.Application;

public readonly record struct SpriteHotReloadMetrics(
    int RefreshedStaticSpriteComponentCount,
    int RefreshedAnimatedSpriteComponentCount,
    int InvalidatedViewCount,
    double ElapsedMilliseconds);