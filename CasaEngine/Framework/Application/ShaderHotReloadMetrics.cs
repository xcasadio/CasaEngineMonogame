namespace CasaEngine.Framework.Application;

public readonly record struct ShaderHotReloadMetrics(
    string ShaderContentName,
    int ReloadedConsumerCount,
    int InvalidatedViewCount,
    double ElapsedMilliseconds);