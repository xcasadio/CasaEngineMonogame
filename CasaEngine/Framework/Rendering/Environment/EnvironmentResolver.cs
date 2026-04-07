using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Resolves the effective environment data for a view by combining view overrides,
/// world defaults, and legacy fallbacks.
/// </summary>
public static class EnvironmentResolver
{
    public static readonly Vector3 LegacyAmbientColor = new(0.05f, 0.05f, 0.05f);

    public static ResolvedEnvironmentSettings Resolve(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var source = view.EnvironmentOverride ?? view.World.EnvironmentSettings;
        bool usesLegacyClearColor = source.BackgroundMode == EnvironmentBackgroundMode.LegacyClearColor;
        bool hasEnvironmentCubemap = source.BackgroundCubemap is not null || source.BackgroundCubemapAssetId != Guid.Empty;
        bool usesLegacyLighting = source.Type == EnvironmentType.None
            && source.EnvironmentAssetId == Guid.Empty
            && source.SpecularEnvironmentCubemap is null
            && !hasEnvironmentCubemap;

        if (!usesLegacyClearColor
            && source.BackgroundMode == EnvironmentBackgroundMode.Environment
            && !hasEnvironmentCubemap)
        {
            usesLegacyClearColor = true;
        }

        var backgroundColor = usesLegacyClearColor
            ? view.ClearColor
            : source.BackgroundColor;

        return new ResolvedEnvironmentSettings
        {
            Type = source.Type,
            BackgroundMode = usesLegacyClearColor ? EnvironmentBackgroundMode.LegacyClearColor : source.BackgroundMode,
            BackgroundColor = backgroundColor,
            EnvironmentAssetId = source.EnvironmentAssetId,
            BackgroundCubemapAssetId = source.BackgroundCubemapAssetId,
            BackgroundCubemap = hasEnvironmentCubemap ? source.BackgroundCubemap : null,
            SpecularEnvironmentCubemap = source.SpecularEnvironmentCubemap,
            AmbientColor = usesLegacyLighting ? LegacyAmbientColor : source.AmbientColor,
            AmbientIntensity = source.AmbientIntensity,
            SpecularIntensity = source.SpecularIntensity,
            UsesLegacyClearColor = usesLegacyClearColor,
            UsesLegacyLighting = usesLegacyLighting,
        };
    }
}