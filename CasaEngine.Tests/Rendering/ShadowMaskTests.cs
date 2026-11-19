using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The shadow mask of a material: <see cref="MaterialBase.ShadowMaskTexture"/> is what <c>ShadowPass</c> binds to cut a shadow
/// out, with <see cref="MaterialBase.AlphaCutoff"/>, instead of a type test on each material class.
/// </summary>
public class ShadowMaskTests
{
    [Fact]
    public void ShadowPass_ReadsTheMaskFromTheMaterial_NotFromItsType()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework", "Rendering", "Draw", "ShadowPass.cs"));

        Assert.Contains(".ShadowMaskTexture", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LitDiffuseMaterial", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnlitTextureMaterial", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LitPbrMaterial", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialsWithoutTexture_ProvideNoMask_InEveryQueue()
    {
        foreach (RenderQueue queue in new[] { RenderQueue.Opaque, RenderQueue.AlphaTest, RenderQueue.Transparent })
        {
            Assert.Null(new LitPbrMaterial { Queue = queue }.ShadowMaskTexture);
            Assert.Null(new LitDiffuseMaterial { Queue = queue }.ShadowMaskTexture);
            Assert.Null(new UnlitTextureMaterial { Queue = queue }.ShadowMaskTexture);
        }
    }

    [GpuFact]
    public void LitPbrMaterial_ProvidesItsBaseColorTexture_OnlyInTheAlphaTestQueue()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var baseTexture = new Texture2D(GpuDeviceHost.Instance.GraphicsDevice, 1, 1);
            using var otherTexture = new Texture2D(GpuDeviceHost.Instance.GraphicsDevice, 1, 1);

            var material = new LitPbrMaterial
            {
                Queue = RenderQueue.AlphaTest,
                AlphaCutoff = 0.35f,
                BaseColorTexture = baseTexture,
                NormalTexture = otherTexture,
                MetallicRoughnessTexture = otherTexture,
                OcclusionTexture = otherTexture,
                EmissiveTexture = otherTexture,
            };

            Assert.Same(baseTexture, material.ShadowMaskTexture);
            Assert.Equal(0.35f, material.AlphaCutoff);

            material.Queue = RenderQueue.Opaque;
            Assert.Null(material.ShadowMaskTexture);

            material.Queue = RenderQueue.Transparent;
            Assert.Null(material.ShadowMaskTexture);

            material.Queue = RenderQueue.AlphaTest;
            material.BaseColorTexture = null;
            Assert.Null(material.ShadowMaskTexture);
        });
    }

    [GpuFact]
    public void LitDiffuseAndUnlitTexture_ProvideExactlyWhatTheShadowPassUsedToBind()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var texture = new Texture2D(GpuDeviceHost.Instance.GraphicsDevice, 1, 1);
            using var detail = new Texture2D(GpuDeviceHost.Instance.GraphicsDevice, 1, 1);

            var litDiffuse = new LitDiffuseMaterial { Queue = RenderQueue.AlphaTest, BasColor = texture, NormalMap = detail, DetailTexture = detail };
            var unlit = new UnlitTextureMaterial { Queue = RenderQueue.AlphaTest, BasColor = texture };

            Assert.Same(texture, litDiffuse.ShadowMaskTexture);
            Assert.Same(texture, unlit.ShadowMaskTexture);

            litDiffuse.Queue = RenderQueue.Opaque;
            unlit.Queue = RenderQueue.Transparent;
            Assert.Null(litDiffuse.ShadowMaskTexture);
            Assert.Null(unlit.ShadowMaskTexture);

            litDiffuse.Queue = RenderQueue.AlphaTest;
            litDiffuse.BasColor = null;
            Assert.Null(litDiffuse.ShadowMaskTexture);
        });
    }

    [Fact]
    public void BaseMaterial_ProvidesNoMask()
    {
        Assert.Null(new NoMaskMaterial { Queue = RenderQueue.AlphaTest }.ShadowMaskTexture);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }

    private sealed class NoMaskMaterial : MaterialBase
    {
        public override void Bind(CasaEngine.Framework.Rendering.Shaders.ShaderWrapper shader, in CasaEngine.Framework.Rendering.RenderContext context, Microsoft.Xna.Framework.Matrix world)
        {
        }
    }
}
