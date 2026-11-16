using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T6.3 of the PBR plan: the compiled <c>LitPbr</c> effect keeps its image-based-lighting constants and textures, and the binder
/// serves a shader that declares them (the positive case that T6.2 could only test on the pure resolution). The pixels the shader
/// writes are measured by the demos (<c>CASAENGINE_COLOR_DEMO_IBL=1</c>), not here. Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class LitPbrIblGpuTests
{
    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX, CubeMapFace.NegativeX, CubeMapFace.PositiveY,
        CubeMapFace.NegativeY, CubeMapFace.PositiveZ, CubeMapFace.NegativeZ,
    ];

    [GpuFact]
    public void CompiledEffect_KeepsTheIblConstantsAndTheThreeIblTextures()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);

            Assert.NotNull(effect.Parameters[ShaderParameterNames.HasIbl]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.IblForceBaseLevel]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.IrradianceCubeTexture]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.PrefilteredSpecularAtlasTexture]);
            Assert.NotNull(effect.Parameters[ShaderParameterNames.BrdfLookupTexture]);
        });
    }

    // A shader that declares HasIbl: the first binding registers the request and leaves the flat ambient (HasIbl 0, slots empty);
    // after the flush the next binding, with the same resolved environment, binds the three textures and sets HasIbl to 1.
    [GpuFact]
    public void BindingOnALitPbrShader_RegistersTheRequestThenBindsTheGeneratedTextures()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            var generatorEffect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName);
            using var generator = new IblCubeGenerator(GpuDeviceHost.Instance.GraphicsDevice, generatorEffect);
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(8, new Vector4(0.8f, 0.4f, 0.2f, 1.0f));
            var environment = new ResolvedEnvironmentSettings { SpecularEnvironmentCubemap = source, SpecularEnvironmentCubemapIsLinear = true };

            EnvironmentShaderBinder.BindImageBasedLighting(shader, in environment, null, cache);

            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasIbl].GetValueSingle());
            Assert.Null(effect.Parameters[ShaderParameterNames.IrradianceCubeTexture].GetValueTextureCube());
            Assert.Equal(1, cache.PendingRequestCount);

            cache.Flush(generator);
            EnvironmentShaderBinder.BindImageBasedLighting(shader, in environment, null, cache);

            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasIbl].GetValueSingle());
            Assert.NotNull(effect.Parameters[ShaderParameterNames.IrradianceCubeTexture].GetValueTextureCube());
            Assert.NotNull(effect.Parameters[ShaderParameterNames.PrefilteredSpecularAtlasTexture].GetValueTexture2D());
            Assert.NotNull(effect.Parameters[ShaderParameterNames.BrdfLookupTexture].GetValueTexture2D());
            Assert.Equal(1, cache.GenerationCount);
            Assert.Equal(0, cache.PendingRequestCount);

            // A second binding with the same environment neither requests nor generates again.
            EnvironmentShaderBinder.BindImageBasedLighting(shader, in environment, null, cache);
            Assert.Equal(1, cache.GenerationCount);
            Assert.Equal(0, cache.PendingRequestCount);

            // Without a cube the slots are emptied and HasIbl goes back to 0.
            var noCube = new ResolvedEnvironmentSettings();
            EnvironmentShaderBinder.BindImageBasedLighting(shader, in noCube, null, cache);
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasIbl].GetValueSingle());
            Assert.Null(effect.Parameters[ShaderParameterNames.PrefilteredSpecularAtlasTexture].GetValueTexture2D());
        });
    }

    private static TextureCube CreateUniformCube(int size, Vector4 color)
    {
        var cube = new TextureCube(GpuDeviceHost.Instance.GraphicsDevice, size, false, SurfaceFormat.Vector4);
        var texels = new Vector4[size * size];
        Array.Fill(texels, color);
        foreach (CubeMapFace face in Faces)
        {
            cube.SetData(face, texels);
        }

        return cube;
    }
}
