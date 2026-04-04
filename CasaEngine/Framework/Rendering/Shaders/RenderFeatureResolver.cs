using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Aggregates all inputs that can influence the effective shader feature set of a draw.
/// Material-driven, mesh-driven, and draw-path-driven features are resolved in a single place
/// so renderers do not duplicate feature calculation rules.
/// </summary>
public readonly struct RenderFeatureInput
{
    public MaterialBase Material { get; init; }

    public StaticModelMesh? Mesh { get; init; }

    public bool IsSkinned { get; init; }

    public bool IsInstanced { get; init; }

    public bool HasVertexColor { get; init; }
}

/// <summary>
/// Central entry point for computing shader features for a draw item.
/// The initial implementation preserves existing behaviour while giving the renderer a
/// stable surface for later migration of material, mesh, and draw-path rules.
/// </summary>
public static class RenderFeatureResolver
{
    public static ShaderFeature Resolve(in RenderFeatureInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Material);

        var features = input.Material.GetFeatures(input.Mesh);

        if (input.IsSkinned)
        {
            features |= ShaderFeature.Skinned;
        }

        if (input.IsInstanced)
        {
            features |= ShaderFeature.Instanced;
        }

        if (input.HasVertexColor)
        {
            features |= ShaderFeature.VertexColor;
        }

        return features;
    }

    public static ShaderFeature Resolve(
        MaterialBase material,
        StaticModelMesh? mesh = null,
        bool isSkinned = false,
        bool isInstanced = false,
        bool hasVertexColor = false)
        => Resolve(new RenderFeatureInput
        {
            Material = material,
            Mesh = mesh,
            IsSkinned = isSkinned,
            IsInstanced = isInstanced,
            HasVertexColor = hasVertexColor,
        });
}