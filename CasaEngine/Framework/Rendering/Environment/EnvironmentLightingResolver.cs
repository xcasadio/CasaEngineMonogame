namespace CasaEngine.Framework.Rendering.Environment;

public static class EnvironmentLightingResolver
{
    public static void Resolve(LightingContext target, in ResolvedEnvironmentSettings environment)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.ClearLights();
        target.AmbientColor = environment.UsesLegacyLighting
            ? EnvironmentResolver.LegacyAmbientColor
            : environment.EffectiveAmbientColor;
    }

    public static void ApplyLegacyLighting(LightingContext target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.ClearLights();
        target.AmbientColor = EnvironmentResolver.LegacyAmbientColor;
    }
}