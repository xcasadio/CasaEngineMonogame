using XnaTextureCube = Microsoft.Xna.Framework.Graphics.TextureCube;

namespace CasaEngine.Framework.Rendering.Environment;

internal static class ReflectionProbeResolver
{
    public static ResolvedReflectionProbeBlend Resolve(RenderView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.World.ReflectionProbes.Count == 0)
        {
            return default;
        }

        Span<ResolvedReflectionProbe> selectedProbes = stackalloc ResolvedReflectionProbe[ReflectionProbeSelector.MaxSelectedProbes];
        int selectedCount = ReflectionProbeSelector.Select(view.Camera.Position, view.World.ReflectionProbes, selectedProbes, out float localInfluence);
        if (selectedCount == 0 || localInfluence <= 0.0f)
        {
            return default;
        }

        Guid primaryProbeId = selectedProbes[0].ProbeId;
        XnaTextureCube primaryCubemap = ResolveCubemap(view, selectedProbes[0]);
        float primaryWeight = primaryCubemap is not null ? selectedProbes[0].Weight : 0.0f;

        Guid secondaryProbeId = Guid.Empty;
        XnaTextureCube secondaryCubemap = null;
        float secondaryWeight = 0.0f;

        if (selectedCount > 1)
        {
            secondaryProbeId = selectedProbes[1].ProbeId;
            secondaryCubemap = ResolveCubemap(view, selectedProbes[1]);
            secondaryWeight = secondaryCubemap is not null ? selectedProbes[1].Weight : 0.0f;
        }

        if (primaryCubemap is null && secondaryCubemap is not null)
        {
            primaryProbeId = secondaryProbeId;
            primaryCubemap = secondaryCubemap;
            primaryWeight = secondaryWeight;
            secondaryProbeId = Guid.Empty;
            secondaryCubemap = null;
            secondaryWeight = 0.0f;
        }

        if (primaryCubemap is null)
        {
            return default;
        }

        float localWeightTotal = primaryWeight + secondaryWeight;
        if (localWeightTotal > 0.0f)
        {
            primaryWeight /= localWeightTotal;
            secondaryWeight /= localWeightTotal;
        }

        return new ResolvedReflectionProbeBlend
        {
            PrimaryProbeId = primaryProbeId,
            SecondaryProbeId = secondaryProbeId,
            PrimaryCubemap = primaryCubemap,
            SecondaryCubemap = secondaryCubemap,
            PrimaryWeight = primaryWeight,
            SecondaryWeight = secondaryWeight,
            Influence = localInfluence,
        };
    }

    private static XnaTextureCube ResolveCubemap(RenderView view, in ResolvedReflectionProbe probe)
    {
        if (probe.SpecularCubemapAssetId != Guid.Empty)
        {
            return EnvironmentAssetLookup.TryLoadTextureCube(view, probe.SpecularCubemapAssetId);
        }

        if (probe.EnvironmentAssetId == Guid.Empty)
        {
            return null;
        }

        EnvironmentAsset environmentAsset = EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, probe.EnvironmentAssetId);
        if (environmentAsset is null)
        {
            return null;
        }

        if (environmentAsset.PanoramaAssetId != Guid.Empty)
        {
            return PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, environmentAsset.PanoramaAssetId, environmentAsset.PanoramaCubemapSize);
        }

        Guid cubemapAssetId = environmentAsset.SpecularCubemapAssetId != Guid.Empty
            ? environmentAsset.SpecularCubemapAssetId
            : environmentAsset.BackgroundCubemapAssetId;
        return EnvironmentAssetLookup.TryLoadTextureCube(view, cubemapAssetId);
    }
}

internal readonly struct ResolvedReflectionProbeBlend
{
    public Guid PrimaryProbeId { get; init; }

    public Guid SecondaryProbeId { get; init; }

    public XnaTextureCube PrimaryCubemap { get; init; }

    public XnaTextureCube SecondaryCubemap { get; init; }

    public float PrimaryWeight { get; init; }

    public float SecondaryWeight { get; init; }

    public float Influence { get; init; }
}