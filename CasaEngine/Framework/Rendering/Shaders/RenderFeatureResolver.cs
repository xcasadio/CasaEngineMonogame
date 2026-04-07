using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    public RiggedModel.RiggedModelMesh? SkinnedMesh { get; init; }

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

        var features = ResolveMaterialFeatures(input.Material);
        bool isSkinned = input.IsSkinned || input.SkinnedMesh is not null;

        if ((features & ShaderFeature.NormalMap) != 0 && !SupportsNormalMapping(input, isSkinned))
        {
            features &= ~ShaderFeature.NormalMap;
        }

        bool hasVertexColor = input.HasVertexColor || HasVertexColor(input.Mesh) || HasVertexColor(input.SkinnedMesh);

        if (isSkinned)
        {
            features |= ShaderFeature.Skinned;
        }

        if (input.IsInstanced)
        {
            features |= ShaderFeature.Instanced;
        }

        if (hasVertexColor)
        {
            features |= ShaderFeature.VertexColor;
        }

        return features;
    }

    public static ShaderFeature ResolveMaterialFeatures(MaterialBase material)
    {
        ArgumentNullException.ThrowIfNull(material);

        var features = ShaderFeature.None;
        bool hasBasColorTexture = HasBasColorTexture(material);

        if (hasBasColorTexture)
        {
            features |= ShaderFeature.BasColorTexture;
        }

        if (hasBasColorTexture && HasNormalMap(material))
        {
            features |= ShaderFeature.NormalMap;
        }

        if (HasEmissive(material))
        {
            features |= ShaderFeature.Emissive;
        }

        if (HasReflection(material))
        {
            features |= ShaderFeature.Reflection;
        }

        if (IsAlphaTest(material))
        {
            features |= ShaderFeature.AlphaTest;
        }

        if (IsTransparent(material))
        {
            features |= ShaderFeature.Transparent;
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

    public static ShaderFeature ResolveSkinned(
        MaterialBase material,
        RiggedModel.RiggedModelMesh skinnedMesh,
        bool isInstanced = false)
        => Resolve(new RenderFeatureInput
        {
            Material = material,
            SkinnedMesh = skinnedMesh,
            IsSkinned = true,
            IsInstanced = isInstanced,
        });

    public static ShaderFeature AddInstancedFeature(ShaderFeature features)
        => features | ShaderFeature.Instanced;

    private static bool HasBasColorTexture(MaterialBase material) => material switch
    {
        LitDiffuseMaterial lit => lit.BasColor is not null || lit.BasColorAssetId != Guid.Empty,
        UnlitTextureMaterial unlit => unlit.BasColor is not null || unlit.BasColorAssetId != Guid.Empty,
        Material rich => rich.TextureBaseColor?.Resource is not null || rich.TextureBaseColorAssetId != Guid.Empty,
        _ => false,
    };

    private static bool HasNormalMap(MaterialBase material) => material switch
    {
        LitDiffuseMaterial lit => lit.NormalMap is not null || lit.NormalMapAssetId != Guid.Empty,
        Material rich => rich.TextureNormal?.Resource is not null || rich.TextureNormalAssetId != Guid.Empty,
        _ => false,
    };

    private static bool HasEmissive(MaterialBase material) => material switch
    {
        LitDiffuseMaterial lit => lit.EmissiveColor != Vector3.Zero,
        _ => false,
    };

    private static bool HasReflection(MaterialBase material) => material switch
    {
        LitDiffuseMaterial lit => lit.ReflectionCube is not null || lit.ReflectionCubeAssetId != Guid.Empty,
        _ => false,
    };

    private static bool IsAlphaTest(MaterialBase material)
        => material.Queue == RenderQueue.AlphaTest;

    private static bool IsTransparent(MaterialBase material)
    {
        if (material.Queue == RenderQueue.AlphaTest)
        {
            return false;
        }

        if (material.IsTransparent || material.Queue >= RenderQueue.Transparent)
        {
            return true;
        }

        if (ReferenceEquals(material.BlendState, BlendState.AlphaBlend) ||
            ReferenceEquals(material.BlendState, BlendState.NonPremultiplied) ||
            ReferenceEquals(material.BlendState, BlendState.Additive))
        {
            return true;
        }

        return material switch
        {
            UnlitTextureMaterial { Alpha: < 0.999f } => true,
            UnlitTextureMaterial unlit when unlit.Tint.A < byte.MaxValue => true,
            LitDiffuseMaterial lit when lit.DiffuseColor.A < byte.MaxValue => true,
            _ => false,
        };
    }

    private static bool HasVertexColor(StaticModelMesh? mesh)
        => mesh?.VertexBuffer?.VertexDeclaration is { } vertexDeclaration &&
           HasVertexElement(vertexDeclaration, VertexElementUsage.Color);

    private static bool SupportsNormalMapping(in RenderFeatureInput input, bool isSkinned)
    {
        if (isSkinned || input.SkinnedMesh is not null)
        {
            return false;
        }

        return HasTangents(input.Mesh);
    }

    private static bool HasTangents(StaticModelMesh? mesh)
        => mesh?.HasTangents == true ||
           mesh?.VertexBuffer?.VertexDeclaration is { } vertexDeclaration &&
           HasVertexElement(vertexDeclaration, VertexElementUsage.Tangent);

    private static bool HasVertexColor(RiggedModel.RiggedModelMesh? mesh)
        => mesh?.HasVertexColors ?? false;

    private static bool HasVertexElement(VertexDeclaration vertexDeclaration, VertexElementUsage usage)
    {
        foreach (var element in vertexDeclaration.GetVertexElements())
        {
            if (element.VertexElementUsage == usage)
            {
                return true;
            }
        }

        return false;
    }
}