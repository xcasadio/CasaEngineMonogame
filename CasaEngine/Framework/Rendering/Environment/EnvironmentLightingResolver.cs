using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

public static class EnvironmentLightingResolver
{
    private static readonly LightingContext LegacyLighting = CreateLegacyLighting();

    public static void Resolve(LightingContext target, in ResolvedEnvironmentSettings environment)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.CopyFrom(LegacyLighting);
        if (!environment.UsesLegacyLighting)
        {
            target.AmbientColor = environment.EffectiveAmbientColor;
        }
    }

    public static void ApplyLegacyLighting(LightingContext target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.CopyFrom(LegacyLighting);
    }

    private static LightingContext CreateLegacyLighting()
    {
        var lighting = new LightingContext
        {
            ActiveDirectionalLightCount = 3,
            AmbientColor = EnvironmentResolver.LegacyAmbientColor,
        };

        lighting.DirectionalLights[0] = new DirectionalLight(
            new Vector3(-0.5265408f, -0.5735765f, -0.6275069f),
            new Vector3(0.92f, 0.92f, 0.92f),
            new Vector3(0.92f, 0.92f, 0.92f));
        lighting.DirectionalLights[1] = new DirectionalLight(
            new Vector3(0.7198464f, 0.3420201f, 0.6040227f),
            new Vector3(0.71f, 0.71f, 0.71f),
            Vector3.Zero);
        lighting.DirectionalLights[2] = new DirectionalLight(
            new Vector3(0.4545195f, -0.7660444f, 0.4545195f),
            new Vector3(0.36f, 0.36f, 0.36f),
            new Vector3(0.36f, 0.36f, 0.36f));
        return lighting;
    }
}