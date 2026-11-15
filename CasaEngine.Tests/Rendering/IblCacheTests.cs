using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// T6.2 of the PBR plan: the cache of the IBL textures (ADR-0109) on a real device. A request is registered by the binder, executed
/// by <see cref="IblCache.Flush"/> outside the view loop, generated once per source and released with its source. Skipped where no
/// device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class IblCacheTests
{
    private static readonly CubeMapFace[] Faces =
    [
        CubeMapFace.PositiveX, CubeMapFace.NegativeX, CubeMapFace.PositiveY,
        CubeMapFace.NegativeY, CubeMapFace.PositiveZ, CubeMapFace.NegativeZ,
    ];

    private readonly ITestOutputHelper _output;

    public IblCacheTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Test (1): request, flush, then the next binding of a shader that declares the IBL gets non-null textures, with no change of
    // environment (same source instance, same resolved value).
    [GpuFact]
    public void ARequest_IsServedAfterTheFlush_WithoutAnyChangeOfEnvironment()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(16, new Vector4(0.8f, 0.4f, 0.2f, 1.0f));
            var environment = new ResolvedEnvironmentSettings { SpecularEnvironmentCubemap = source, SpecularEnvironmentCubemapIsLinear = true };

            Assert.False(EnvironmentShaderBinder.TryResolveImageBasedLighting(in environment, cache, out TextureCube irradiance, out Texture2D atlas, out Texture2D brdf));
            Assert.Null(irradiance);
            Assert.Null(atlas);
            Assert.Null(brdf);
            Assert.Equal(1, cache.PendingRequestCount);
            Assert.Equal(0, cache.GenerationCount);

            cache.Flush(generator);

            Assert.Equal(0, cache.PendingRequestCount);
            Assert.True(EnvironmentShaderBinder.TryResolveImageBasedLighting(in environment, cache, out irradiance, out atlas, out brdf));
            Assert.NotNull(irradiance);
            Assert.NotNull(atlas);
            Assert.NotNull(brdf);
            Assert.Same(cache.BrdfTable, brdf);
            Assert.Equal(IblCubeGenerator.IrradianceSize, irradiance.Size);
            Assert.Equal(IblCubeGenerator.AtlasHeight, atlas.Height);
            Assert.Equal(BrdfLookupTable.Size, brdf.Width);
            _output.WriteLine($"BRDF table: {cache.BrdfTableMilliseconds:F1} ms; one generation of a 16 px source: irradiance and atlas {generator.LastGenerationMilliseconds:F1} ms (atlas)");
        });
    }

    // Test (2): a shader that does not declare the IBL (LitForward) registers no request.
    [GpuFact]
    public void AShaderWithoutIblParameters_RegistersNoRequest()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.LitForwardContentName);
            var shader = new ShaderWrapper(effect);
            Assert.False(shader.HasParameter(ShaderParameterNames.HasIbl));
            Assert.False(shader.HasParameter(ShaderParameterNames.IrradianceCubeTexture));

            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(8, Vector4.One);
            var environment = new ResolvedEnvironmentSettings { SpecularEnvironmentCubemap = source };

            EnvironmentShaderBinder.Bind(shader, in environment, null, cache);

            Assert.Equal(0, cache.PendingRequestCount);
            Assert.Equal(0, cache.EntryCount);
        });
    }

    // Test (3): one generation per source, however many times it is requested or flushed.
    [GpuFact]
    public void ASource_IsGeneratedOnlyOnce()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var first = CreateUniformCube(8, new Vector4(1, 0, 0, 1));
            using var second = CreateUniformCube(8, new Vector4(0, 1, 0, 1));

            for (int frame = 0; frame < 3; frame++)
            {
                cache.TryGet(first, true, out _, out _, out _);
                cache.TryGet(first, true, out _, out _, out _);
                cache.Flush(generator);
                cache.Flush(generator);
            }

            Assert.Equal(1, cache.GenerationCount);
            Assert.True(cache.TryGet(first, true, out TextureCube irradianceOfFirst, out Texture2D atlasOfFirst, out _));

            cache.TryGet(second, true, out _, out _, out _);
            cache.Flush(generator);
            Assert.Equal(2, cache.GenerationCount);
            Assert.True(cache.TryGet(second, true, out TextureCube irradianceOfSecond, out _, out _));
            Assert.NotSame(irradianceOfFirst, irradianceOfSecond);

            // The first source kept its textures.
            Assert.True(cache.TryGet(first, true, out TextureCube again, out Texture2D atlasAgain, out _));
            Assert.Same(irradianceOfFirst, again);
            Assert.Same(atlasOfFirst, atlasAgain);
            Assert.True(cache.GeneratedOutsideViewLoop);
        });
    }

    // Test (4): the source is disposed; at the next flush both generated textures are disposed and the entry is gone.
    [GpuFact]
    public void ADisposedSource_ReleasesItsTexturesAtTheNextFlush()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            var source = CreateUniformCube(8, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));

            cache.TryGet(source, true, out _, out _, out _);
            cache.Flush(generator);
            Assert.True(cache.TryGet(source, true, out TextureCube irradiance, out Texture2D atlas, out Texture2D brdf));
            Assert.Equal(1, cache.EntryCount);

            source.Dispose();
            cache.Flush(generator);

            Assert.True(irradiance.IsDisposed);
            Assert.True(atlas.IsDisposed);
            Assert.False(brdf.IsDisposed);
            Assert.Equal(0, cache.EntryCount);
            Assert.Equal(0, cache.PendingRequestCount);
        });
    }

    [GpuFact]
    public void DisposingTheCache_ReleasesEveryTexture()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(8, Vector4.One);
            cache.TryGet(source, true, out _, out _, out _);
            cache.Flush(generator);
            cache.TryGet(source, true, out TextureCube irradiance, out Texture2D atlas, out Texture2D brdf);

            cache.Dispose();

            Assert.True(irradiance.IsDisposed);
            Assert.True(atlas.IsDisposed);
            Assert.True(brdf.IsDisposed);
            Assert.False(cache.TryGet(source, true, out _, out _, out _));
        });
    }

    // Without the convolution effect (no generator) nothing is generated and the request stays registered.
    [GpuFact]
    public void WithoutAGenerator_NothingIsGenerated_AndTheRequestIsKept()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(8, Vector4.One);

            cache.TryGet(source, true, out _, out _, out _);
            cache.Flush(null);
            cache.Flush(null);

            Assert.Equal(0, cache.GenerationCount);
            Assert.Equal(1, cache.PendingRequestCount);
            Assert.False(cache.TryGet(source, true, out _, out _, out _));
            Assert.Null(cache.BrdfTable);
        });
    }

    // A null cube (no specular environment) or no cache registers nothing.
    [GpuFact]
    public void WithoutACubeOrACache_NothingIsRequested()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            var noCube = new ResolvedEnvironmentSettings();
            Assert.False(EnvironmentShaderBinder.TryResolveImageBasedLighting(in noCube, cache, out _, out _, out _));
            Assert.Equal(0, cache.PendingRequestCount);

            using var source = CreateUniformCube(8, Vector4.One);
            var withCube = new ResolvedEnvironmentSettings { SpecularEnvironmentCubemap = source };
            Assert.False(EnvironmentShaderBinder.TryResolveImageBasedLighting(in withCube, null, out _, out _, out _));
            Assert.Equal(0, cache.PendingRequestCount);
        });
    }

    // The linear flag recorded with the request reaches the generator: a non-linear 0.5 gives 0.5^2.2 in the irradiance.
    [GpuFact]
    public void TheLinearFlagOfTheSource_ReachesTheGenerator()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(8, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));

            cache.TryGet(source, sourceIsLinear: false, out _, out _, out _);
            cache.Flush(generator);
            Assert.True(cache.TryGet(source, false, out TextureCube irradiance, out _, out _));

            var texels = new Vector4[IblCubeGenerator.IrradianceSize * IblCubeGenerator.IrradianceSize];
            irradiance.GetData(CubeMapFace.PositiveX, texels);
            float expected = MathF.Pow(0.5f, 2.2f);
            Assert.InRange(texels[texels.Length / 2].X, expected * 0.98f, expected * 1.02f);
        });
    }

    // The generation ran from Flush: when the pipeline says its view loop is active, the indicator records a generation inside it.
    [GpuFact]
    public void AGenerationInsideTheViewLoop_IsReported()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            using var generator = CreateGenerator();
            using var cache = new IblCache(GpuDeviceHost.Instance.GraphicsDevice);
            using var source = CreateUniformCube(8, Vector4.One);

            cache.ViewLoopActive = true;
            cache.TryGet(source, true, out _, out _, out _);
            cache.Flush(generator);

            Assert.Equal(1, cache.GenerationCount);
            Assert.False(cache.GeneratedOutsideViewLoop);
        });
    }

    private static IblCubeGenerator CreateGenerator()
    {
        var effect = GpuDeviceHost.Instance.Game.Content.Load<Effect>(BuiltInShaderCatalog.IblConvolutionContentName);
        return new IblCubeGenerator(GpuDeviceHost.Instance.GraphicsDevice, effect);
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

/// <summary>
/// T6.2, parts that need no device: where the requests are made and run (ADR-0109). The request point is the binder, never the
/// resolver (its value is cached per view); the execution point is the start of <c>RenderPipeline.Render</c>, before the loop of
/// the views; nothing but the pipeline flushes the cache.
/// </summary>
public class IblCacheCoverageTests
{
    private static string Read(params string[] relative) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), "CasaEngine", "Framework", .. relative])).Replace("\r\n", "\n");

    [Fact]
    public void TheResolverAndItsResolvedValue_KnowNothingOfTheIblCache()
    {
        Assert.DoesNotContain("IblCache", Read("Rendering", "Environment", "EnvironmentResolver.cs"));
        Assert.DoesNotContain("IblCache", Read("Rendering", "Environment", "ResolvedEnvironmentSettings.cs"));
        Assert.DoesNotContain("IrradianceCubemap", Read("Rendering", "Environment", "ResolvedEnvironmentSettings.cs"));
    }

    [Fact]
    public void TheRequestsRunAtTheStartOfRender_BeforeTheLoopOfTheViews()
    {
        string pipeline = Read("Rendering", "RenderPipeline.cs");
        int flush = pipeline.IndexOf("_iblCache.Flush(_iblCubeGenerator);", StringComparison.Ordinal);
        int firstTarget = pipeline.IndexOf("DrawWorldUIToTextures();", StringComparison.Ordinal);
        int viewLoop = pipeline.IndexOf("foreach (var view in orderedViews)", StringComparison.Ordinal);
        int renderStart = pipeline.IndexOf("public void Render(IReadOnlyList<RenderView> views", StringComparison.Ordinal);

        Assert.True(renderStart > 0 && flush > renderStart, "the flush is inside Render");
        Assert.True(flush < firstTarget, "the flush comes before any other target work");
        Assert.True(flush < viewLoop, "the flush comes before the loop of the views");
        Assert.Contains("with { Ibl = _iblCache }", pipeline);
    }

    [Fact]
    public void OnlyTheRenderPipeline_FlushesTheCache()
    {
        string root = Path.Combine(FindRepositoryRoot(), "CasaEngine", "Framework");
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) is "RenderPipeline.cs" or "IblCache.cs")
            {
                continue;
            }

            string text = File.ReadAllText(file);
            Assert.DoesNotContain("IblCache.Flush", text);
            Assert.DoesNotContain("_iblCache.Flush", text);
        }
    }

    [Fact]
    public void BothRenderersPassTheCacheToTheirContext_AndTheShaderBindCacheToTheBinder()
    {
        Assert.Contains("Ibl = frame.Ibl,", Read("Application", "Components", "StaticMeshRendererComponent.cs"));
        Assert.Contains("Ibl = frame.Ibl,", Read("Application", "Components", "SkinnedMeshRendererComponent.cs"));
        Assert.Contains("EnvironmentShaderBinder.Bind(shader, in context.Environment, context.Stats, context.Ibl);", Read("Rendering", "Shaders", "ShaderBindCache.cs"));
    }

    [Fact]
    public void TheBinderTestsTheShaderForTheIblParameters_BeforeRequestingAnything()
    {
        string binder = Read("Rendering", "Environment", "EnvironmentShaderBinder.cs");
        int declared = binder.IndexOf("shader.HasParameter(ShaderParameterNames.HasIbl)", StringComparison.Ordinal);
        int request = binder.IndexOf("TryResolveImageBasedLighting(in environment, ibl", StringComparison.Ordinal);
        Assert.True(declared > 0 && request > declared);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.Editor.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}
