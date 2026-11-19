using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T7.1 of the PBR plan: the local reflection probes of <c>lit-pbr</c> (ADR-0110, section Reflection probes). The blend of probes is
/// recomputed at every resolution (it depends on the camera and on the probe collection, not on the cached environment), the
/// probe cubes go through <see cref="IblCache"/> like the global one (atlas only), and a shader that declares the IBL receives the
/// prefiltered atlas of a probe once it exists, the unprefiltered cube until then. The pixels are measured by the demo
/// (<c>CASAENGINE_COLOR_DEMO_PROBES=1</c>). Skipped where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class ReflectionProbeIblGpuTests
{
    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX, CubeMapFace.NegativeX, CubeMapFace.PositiveY,
        CubeMapFace.NegativeY, CubeMapFace.PositiveZ, CubeMapFace.NegativeZ,
    ];

    // Test (a): after a first Resolve, moving the camera changes the blend without any change of the environment.
    [GpuFact]
    public void Resolve_RecomputesTheProbeBlendWhenTheCameraMoves()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var cubeA = CreateUniformCube(8, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            using var cubeB = CreateUniformCube(8, new Vector4(0.0f, 0.0f, 1.0f, 1.0f));
            var world = new World();
            world.ReflectionProbes.Add(new ReflectionProbe { Position = Vector3.Zero, InfluenceRadius = 10.0f, BlendDistance = 4.0f, SpecularCubemap = cubeA });
            world.ReflectionProbes.Add(new ReflectionProbe { Position = new Vector3(16.0f, 0.0f, 0.0f), InfluenceRadius = 10.0f, BlendDistance = 4.0f, SpecularCubemap = cubeB });
            var camera = new PerspectiveCameraComponent();
            var view = new RenderView(world, camera, new StubRenderSurface());

            camera.SetPositionAndTarget(Vector3.Zero, Vector3.UnitZ);
            ResolvedEnvironmentSettings full = EnvironmentResolver.Resolve(view);
            Assert.Same(cubeA, full.PrimaryReflectionProbeCubemap);
            Assert.Null(full.SecondaryReflectionProbeCubemap);
            Assert.Equal(1.0f, full.PrimaryReflectionProbeWeight, 3);
            Assert.Equal(1.0f, full.LocalReflectionProbeInfluence, 3);

            // Between the two probes: both fade, the weights split (the environment itself is unchanged and cached).
            camera.SetPositionAndTarget(new Vector3(8.0f, 0.0f, 0.0f), new Vector3(8.0f, 0.0f, 1.0f));
            ResolvedEnvironmentSettings blend = EnvironmentResolver.Resolve(view);
            Assert.NotNull(blend.PrimaryReflectionProbeCubemap);
            Assert.NotNull(blend.SecondaryReflectionProbeCubemap);
            Assert.Equal(0.5f, blend.PrimaryReflectionProbeWeight, 3);
            Assert.Equal(0.5f, blend.SecondaryReflectionProbeWeight, 3);

            // Only in the fade zone of the first probe: its influence drops below 1.
            camera.SetPositionAndTarget(new Vector3(-9.0f, 0.0f, 0.0f), new Vector3(-9.0f, 0.0f, 1.0f));
            ResolvedEnvironmentSettings edge = EnvironmentResolver.Resolve(view);
            Assert.Same(cubeA, edge.PrimaryReflectionProbeCubemap);
            Assert.Equal(0.25f, edge.LocalReflectionProbeInfluence, 3);

            // Out of every probe: none left.
            camera.SetPositionAndTarget(new Vector3(-30.0f, 0.0f, 0.0f), new Vector3(-30.0f, 0.0f, 1.0f));
            ResolvedEnvironmentSettings none = EnvironmentResolver.Resolve(view);
            Assert.Null(none.PrimaryReflectionProbeCubemap);
            Assert.Equal(0.0f, none.LocalReflectionProbeInfluence);
        });
    }

    // Test (b): a probe added (or removed) after a first Resolve is taken into account.
    [GpuFact]
    public void Resolve_TakesIntoAccountAProbeAddedOrRemovedAfterTheFirstResolution()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var cube = CreateUniformCube(8, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            var world = new World();
            var camera = new PerspectiveCameraComponent();
            camera.SetPositionAndTarget(Vector3.Zero, Vector3.UnitZ);
            var view = new RenderView(world, camera, new StubRenderSurface());

            Assert.Null(EnvironmentResolver.Resolve(view).PrimaryReflectionProbeCubemap);
            Assert.Null(EnvironmentResolver.Resolve(view).PrimaryReflectionProbeCubemap);

            var probe = new ReflectionProbe { Position = Vector3.Zero, InfluenceRadius = 10.0f, BlendDistance = 2.0f, SpecularCubemap = cube };
            world.ReflectionProbes.Add(probe);
            ResolvedEnvironmentSettings withProbe = EnvironmentResolver.Resolve(view);
            Assert.Same(cube, withProbe.PrimaryReflectionProbeCubemap);
            Assert.Equal(probe.Id, withProbe.PrimaryReflectionProbeId);

            world.ReflectionProbes.Remove(probe);
            ResolvedEnvironmentSettings withoutProbe = EnvironmentResolver.Resolve(view);
            Assert.Null(withoutProbe.PrimaryReflectionProbeCubemap);
            Assert.Equal(0.0f, withoutProbe.LocalReflectionProbeInfluence);
        });
    }

    // The cube supplied at run time wins over the asset ids, which are not even looked up.
    [GpuFact]
    public void ASuppliedCube_IsUsedBeforeTheAssetIds()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var cube = CreateUniformCube(8, Vector4.One);
            var world = new World();
            world.ReflectionProbes.Add(new ReflectionProbe
            {
                InfluenceRadius = 10.0f,
                BlendDistance = 2.0f,
                SpecularCubemapAssetId = Guid.NewGuid(),
                EnvironmentAssetId = Guid.NewGuid(),
                SpecularCubemap = cube,
            });
            var camera = new PerspectiveCameraComponent();
            camera.SetPositionAndTarget(Vector3.Zero, Vector3.UnitZ);

            ResolvedEnvironmentSettings resolved = EnvironmentResolver.Resolve(new RenderView(world, camera, new StubRenderSurface()));

            Assert.Same(cube, resolved.PrimaryReflectionProbeCubemap);
        });
    }

    // The cache generates only the atlas of a probe cube, once, and completes an entry that later becomes an environment cube.
    [GpuFact]
    public void TheCache_GeneratesOnlyTheAtlasOfAProbeCube_AndTheIrradianceWhenItIsAlsoTheEnvironmentCube()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var cube = CreateUniformCube(8, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));

            Assert.False(cache.TryGetPrefilteredAtlas(cube, false, out Texture2D atlas));
            Assert.Null(atlas);
            Assert.False(cache.TryGetPrefilteredAtlas(cube, false, out _));
            Assert.Equal(1, cache.PendingRequestCount);

            cache.Flush(generator);

            Assert.Equal(1, cache.GenerationCount);
            Assert.True(cache.TryGetPrefilteredAtlas(cube, false, out atlas));
            Assert.Equal(IblCubeGenerator.AtlasHeight, atlas.Height);
            Assert.Equal(0, cache.PendingRequestCount);

            // The same cube is now asked for as an environment cube: only the irradiance is missing.
            Assert.False(cache.TryGet(cube, false, out TextureCube irradiance, out _, out _));
            Assert.Null(irradiance);
            Assert.Equal(1, cache.PendingRequestCount);
            cache.Flush(generator);
            Assert.True(cache.TryGet(cube, false, out irradiance, out Texture2D sameAtlas, out Texture2D brdf));
            Assert.NotNull(irradiance);
            Assert.NotNull(brdf);
            Assert.Same(atlas, sameAtlas);
            Assert.Equal(2, cache.GenerationCount);
        });
    }

    // Test (c): the binding of a shader that declares the IBL. Before the generation the probe flags are 0 and the unprefiltered
    // cubes of t4 / t5 stay bound; after it the flags are 1 and the atlases are bound.
    [GpuFact]
    public void Binding_GivesTheUnprefilteredProbeUntilItsAtlasExists_ThenThePrefilteredAtlas()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitPbrContentName);
            var shader = new ShaderWrapper(effect);
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var global = CreateUniformCube(8, new Vector4(0.0f, 1.0f, 0.0f, 1.0f));
            using var primary = CreateUniformCube(8, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            using var secondary = CreateUniformCube(8, new Vector4(0.0f, 0.0f, 1.0f, 1.0f));
            var environment = new ResolvedEnvironmentSettings
            {
                SpecularEnvironmentCubemap = global,
                SpecularEnvironmentCubemapIsLinear = true,
                PrimaryReflectionProbeCubemap = primary,
                SecondaryReflectionProbeCubemap = secondary,
                PrimaryReflectionProbeWeight = 0.5f,
                SecondaryReflectionProbeWeight = 0.5f,
                LocalReflectionProbeInfluence = 1.0f,
            };

            EnvironmentShaderBinder.Bind(shader, in environment, null, cache);

            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasPrefilteredLocalReflectionProbe].GetValueSingle());
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasPrefilteredSecondaryLocalReflectionProbe].GetValueSingle());
            Assert.Null(effect.Parameters[ShaderParameterNames.PrefilteredLocalReflectionProbeAtlasTexture].GetValueTexture2D());
            Assert.Same(primary, effect.Parameters[ShaderParameterNames.LocalReflectionProbeCubeTexture].GetValueTextureCube());
            Assert.Same(secondary, effect.Parameters[ShaderParameterNames.SecondaryLocalReflectionProbeCubeTexture].GetValueTextureCube());
            Assert.Equal(3, cache.PendingRequestCount);
            Assert.Equal(0, cache.GenerationCount);

            cache.Flush(generator);
            EnvironmentShaderBinder.Bind(shader, in environment, null, cache);

            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasIbl].GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasPrefilteredLocalReflectionProbe].GetValueSingle());
            Assert.Equal(1.0f, effect.Parameters[ShaderParameterNames.HasPrefilteredSecondaryLocalReflectionProbe].GetValueSingle());
            Texture2D primaryAtlas = effect.Parameters[ShaderParameterNames.PrefilteredLocalReflectionProbeAtlasTexture].GetValueTexture2D();
            Texture2D secondaryAtlas = effect.Parameters[ShaderParameterNames.PrefilteredSecondaryLocalReflectionProbeAtlasTexture].GetValueTexture2D();
            Assert.NotNull(primaryAtlas);
            Assert.NotNull(secondaryAtlas);
            Assert.NotSame(primaryAtlas, secondaryAtlas);
            Assert.Same(primary, effect.Parameters[ShaderParameterNames.LocalReflectionProbeCubeTexture].GetValueTextureCube());
            Assert.Equal(3, cache.GenerationCount);
            Assert.Equal(0, cache.PendingRequestCount);

            // A second binding requests and generates nothing more.
            EnvironmentShaderBinder.Bind(shader, in environment, null, cache);
            Assert.Equal(3, cache.GenerationCount);
            Assert.Equal(0, cache.PendingRequestCount);

            // Without probes the flags go back to 0 and the atlas slots are emptied.
            var noProbes = environment with { PrimaryReflectionProbeCubemap = null, SecondaryReflectionProbeCubemap = null };
            EnvironmentShaderBinder.Bind(shader, in noProbes, null, cache);
            Assert.Equal(0.0f, effect.Parameters[ShaderParameterNames.HasPrefilteredLocalReflectionProbe].GetValueSingle());
            Assert.Null(effect.Parameters[ShaderParameterNames.PrefilteredLocalReflectionProbeAtlasTexture].GetValueTexture2D());
        });
    }

    // LitForward declares neither the IBL nor the probe atlases: binding it requests nothing for the probes.
    [GpuFact]
    public void BindingALitForwardShader_RequestsNothingForTheProbes()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitForwardContentName);
            var shader = new ShaderWrapper(effect);
            Assert.False(shader.HasParameter(ShaderParameterNames.HasPrefilteredLocalReflectionProbe));
            Assert.False(shader.HasParameter(ShaderParameterNames.PrefilteredLocalReflectionProbeAtlasTexture));

            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var probe = CreateUniformCube(8, Vector4.One);
            var environment = new ResolvedEnvironmentSettings { PrimaryReflectionProbeCubemap = probe, PrimaryReflectionProbeWeight = 1.0f, LocalReflectionProbeInfluence = 1.0f };

            EnvironmentShaderBinder.Bind(shader, in environment, null, cache);

            Assert.Equal(0, cache.PendingRequestCount);
            Assert.Equal(0, cache.EntryCount);
        });
    }

    private static IblCubeGenerator CreateGenerator()
    {
        var generatorEffect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName);
        return new IblCubeGenerator(GpuDeviceHost.Instance.GraphicsDevice, generatorEffect);
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

    private sealed class StubRenderSurface : IRenderSurface
    {
        public bool IsBackBuffer => false;

        public Rectangle ViewportRect => new(0, 0, 64, 64);

        public RenderTarget2D? RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }
}
