using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// D3: a compiled material and a code-built material with the same values draw with the same
/// features on the same mesh. The item is built the way the static mesh renderer
/// builds it (same feature function, <see cref="RenderFeatureResolver.ResolveForMesh"/>), then read back through <see cref="RenderItem.Features"/>.
/// </summary>
public class RenderItemFeaturesTests
{
    private const ShaderFeature CompiledFeatures = ShaderFeature.BasColorTexture | ShaderFeature.NormalMap;

    private static CompiledMaterial CreateCompiled(ShaderFeature features)
        => new(
            definitionId: "lit-diffuse",
            effectiveShader: new EffectiveShaderReference(Guid.NewGuid()),
            properties: Array.Empty<KeyValuePair<string, MaterialValue>>(),
            features: features);

    private static StaticModelMesh CreateMesh(bool withTangents)
    {
        var mesh = new StaticModelMesh();
        if (withTangents)
        {
            mesh.SetData(
                new[]
                {
                    new VertexPositionNormalTextureTangent(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero, Vector4.UnitX),
                    new VertexPositionNormalTextureTangent(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX, Vector4.UnitX),
                    new VertexPositionNormalTextureTangent(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY, Vector4.UnitX),
                },
                new uint[] { 0, 1, 2 });
        }
        else
        {
            mesh.SetData(
                new[]
                {
                    new VertexPositionNormalTexture(new Vector3(-1f, 0f, 0f), Vector3.Up, Vector2.Zero),
                    new VertexPositionNormalTexture(new Vector3(1f, 0f, 0f), Vector3.Up, Vector2.UnitX),
                    new VertexPositionNormalTexture(new Vector3(0f, 0f, 1f), Vector3.Up, Vector2.UnitY),
                },
                new uint[] { 0, 1, 2 });
        }

        return mesh;
    }

    private static RenderItem BuildItem(MaterialBase material, CompiledMaterial compiled, StaticModelMesh mesh)
        => new()
        {
            Mesh = mesh,
            Material = material,
            CompiledMaterial = compiled,
            Features = RenderFeatureResolver.ResolveForMesh(material, compiled, mesh),
        };

    [Fact]
    public void CompiledMaterial_DropsNormalMap_OnMeshWithoutTangents()
    {
        var item = BuildItem(new LitDiffuseMaterial(), CreateCompiled(CompiledFeatures), CreateMesh(withTangents: false));

        Assert.Equal(ShaderFeature.BasColorTexture, item.Features);
    }

    [Fact]
    public void CompiledMaterial_KeepsNormalMap_OnMeshWithTangents()
    {
        var item = BuildItem(new LitDiffuseMaterial(), CreateCompiled(CompiledFeatures), CreateMesh(withTangents: true));

        Assert.Equal(CompiledFeatures, item.Features);
    }

    [Fact]
    public void CompiledMaterial_GainsVertexColor_WhenDrawHasVertexColors()
    {
        // A static mesh only reports vertex colours through its GPU vertex declaration (needs a
        // GraphicsDevice), so the vertex-colour rule is exercised through the shared API input.
        var features = RenderFeatureResolver.AdjustToMesh(
            CompiledFeatures,
            new RenderFeatureInput { Mesh = CreateMesh(withTangents: true), HasVertexColor = true });
        var item = new RenderItem { CompiledMaterial = CreateCompiled(CompiledFeatures), Features = features };

        Assert.Equal(CompiledFeatures | ShaderFeature.VertexColor, item.Features);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompiledAndCodeBuiltMaterials_DrawWithSameFeatures(bool withTangents)
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
        };
        var compiled = CreateCompiled(RenderFeatureResolver.ResolveMaterialFeatures(material));
        var mesh = CreateMesh(withTangents);

        var fromCompiled = BuildItem(material, compiled, mesh);
        var fromCode = BuildItem(material, null, mesh);

        Assert.Equal(fromCode.Features, fromCompiled.Features);
    }

    [Fact]
    public void AdjustToMesh_MatchesResolve_ForTheSameMaterialFeatures()
    {
        var material = new LitDiffuseMaterial
        {
            BasColorAssetId = Guid.NewGuid(),
            NormalMapAssetId = Guid.NewGuid(),
        };
        var input = new RenderFeatureInput { Material = material, Mesh = CreateMesh(withTangents: false), IsInstanced = true };

        Assert.Equal(
            RenderFeatureResolver.Resolve(input),
            RenderFeatureResolver.AdjustToMesh(RenderFeatureResolver.ResolveMaterialFeatures(material), input));
    }
}
