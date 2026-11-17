using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Scene.World;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// A reflection probe resolves its cube on every frame: an asset that does not load (cube, environment or panorama) must be tried
/// and logged once, and the id of the generated panorama cube must be computed without allocating. The asset catalog, the project
/// path and the loggers are process-global, so these tests run in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class ReflectionProbeFailedAssetTests : IDisposable
{
    private static readonly Guid AssetId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly string _projectDirectory;
    private readonly string? _previousProjectPath;

    public ReflectionProbeFailedAssetTests()
    {
        AssetCatalog.ClearInternal();
        _previousProjectPath = EngineEnvironment.ProjectPath;
        _projectDirectory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);
        EngineEnvironment.ProjectPath = _projectDirectory;
    }

    public void Dispose()
    {
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();
        Directory.Delete(_projectDirectory, recursive: true);
    }

    private sealed class ThrowingLoader : IAssetLoader
    {
        public int Attempts;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Attempts++;
            throw new InvalidOperationException("the asset does not load");
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class CountingLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new EnvironmentAsset();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Entries { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) => Entries.Add(msg);
        public void WriteDebug(string msg) => Entries.Add(msg);
        public void WriteInfo(string msg) => Entries.Add(msg);
        public void WriteWarning(string msg) => Entries.Add(msg);
        public void WriteError(string msg) => Entries.Add(msg);
    }

    private static RenderView CreateView(AssetContentManager assetContentManager)
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        SetBackingField(game, nameof(CasaEngineGame.AssetContentManager), assetContentManager);

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);

        return new RenderView(world, null, null);
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static CapturingLogger StartCapturing()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        return logger;
    }

    [Fact]
    public void TryLoadEnvironmentAsset_AnAcquisitionThatThrows_IsTriedOnceAndLoggedOnce()
    {
        AssetCatalog.AddInternal(new AssetInfo(AssetId) { Name = "broken", FileName = "broken.environment" });
        var manager = new AssetContentManager();
        var loader = new ThrowingLoader();
        manager.RegisterAssetLoader(typeof(EnvironmentAsset), loader);
        var view = CreateView(manager);

        var logger = StartCapturing();
        try
        {
            Assert.Null(EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, AssetId));
            Assert.Null(EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, AssetId));
        }
        finally
        {
            Logs.Close();
        }

        Assert.Equal(1, loader.Attempts);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void TryLoadEnvironmentAsset_AnIdAbsentFromTheCatalog_IsNotRetained()
    {
        var manager = new AssetContentManager();
        var loader = new CountingLoader();
        manager.RegisterAssetLoader(typeof(EnvironmentAsset), loader);
        var view = CreateView(manager);

        Assert.Null(EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, AssetId));
        Assert.Equal(0, loader.Loads);

        // Catalogued afterwards: the id is resolved, so the first miss was not remembered.
        AssetCatalog.AddInternal(new AssetInfo(AssetId) { Name = "late", FileName = "late.environment" });

        Assert.NotNull(EnvironmentAssetLookup.TryLoadEnvironmentAsset(view, AssetId));
        Assert.Equal(1, loader.Loads);
    }

    [Theory]
    [InlineData("11111111-2222-3333-4444-555555555555", 256, "4b06d0cf-2222-3333-4444-555555555577")]
    [InlineData("11111111-2222-3333-4444-555555555555", 16, "4b06d1df-2222-3333-4444-555555555577")]
    [InlineData("77777777-7777-7777-7777-777777777777", 1024, "2d60b3a9-7777-7777-7777-777777777755")]
    [InlineData("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", 512, "fba5010a-e5f6-4a7b-8c9d-0e1f2a3b4c7f")]
    public void CreateGeneratedCubemapAssetId_KeepsTheIdentifiersOfTheEarlierImplementation(string panoramaId, int cubemapSize, string expected)
    {
        Guid generated = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(Guid.Parse(panoramaId), cubemapSize);

        Assert.Equal(Guid.Parse(expected), generated);
    }

    [Fact]
    public void CreateGeneratedCubemapAssetId_AllocatesNothing()
    {
        Guid panoramaId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid sink = Guid.Empty;

        // Warm-up, so that the measure does not include the compilation of the method.
        for (int i = 0; i < 10; i++)
        {
            sink = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(panoramaId, 256);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            sink = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(panoramaId, 256);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotEqual(Guid.Empty, sink);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void GetOrCreateCubemap_APanoramaThatDoesNotDecode_IsTriedOnceAndLoggedOnce()
    {
        AssetCatalog.AddInternal(new AssetInfo(AssetId) { Name = "pano", FileName = "pano.png" });
        File.WriteAllBytes(Path.Combine(_projectDirectory, "pano.png"), [1, 2, 3, 4, 5, 6, 7, 8]);
        var view = CreateView(new AssetContentManager());

        var logger = StartCapturing();
        try
        {
            Assert.Null(PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));
            Assert.Null(PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(logger.Entries);
    }

    [Fact]
    public void GetOrCreateCubemap_AMissingPanoramaFile_IsRememberedWithoutALogEntry()
    {
        AssetCatalog.AddInternal(new AssetInfo(AssetId) { Name = "pano", FileName = "pano.png" });
        var view = CreateView(new AssetContentManager());

        var logger = StartCapturing();
        try
        {
            Assert.Null(PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));
            Assert.Empty(logger.Entries);

            // A file that does not decode appears at the resolved path: the failure of the first resolution is remembered, so the
            // second one neither loads it nor logs.
            File.WriteAllBytes(Path.Combine(_projectDirectory, "pano.png"), [1, 2, 3, 4, 5, 6, 7, 8]);
            Assert.Null(PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));
        }
        finally
        {
            Logs.Close();
        }

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void GetOrCreateCubemap_ACubeAlreadyInTheGeneratedCache_AllocatesNothing()
    {
        var manager = new AssetContentManager();
        var view = CreateView(manager);
        int cubemapSize = PanoramaEnvironmentGenerator.NormalizeCubemapSize(256);
        Guid generatedId = PanoramaEnvironmentGenerator.CreateGeneratedCubemapAssetId(AssetId, cubemapSize);

        // A cube without a GraphicsDevice: the cache only reads its IsDisposed flag.
        var cube = (Microsoft.Xna.Framework.Graphics.TextureCube)RuntimeHelpers.GetUninitializedObject(typeof(Microsoft.Xna.Framework.Graphics.TextureCube));
        GeneratedAssetHandleCache<Microsoft.Xna.Framework.Graphics.TextureCube>.Acquire(manager, generatedId, () => cube, static candidate => candidate.IsDisposed);

        Assert.Same(cube, PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));

        // Warm-up, so that the measure does not include the compilation of the method.
        for (int i = 0; i < 10; i++)
        {
            PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256);
        }

        object? last = null;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            last = PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Same(cube, last);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void GetOrCreateCubemap_APanoramaAbsentFromTheCatalog_IsNotRetained()
    {
        var view = CreateView(new AssetContentManager());

        var logger = StartCapturing();
        try
        {
            Assert.Null(PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));
            Assert.Empty(logger.Entries);

            // Catalogued afterwards, with a file that does not decode: the first miss was not remembered, so it is tried now (and
            // logged, since it does not decode).
            AssetCatalog.AddInternal(new AssetInfo(AssetId) { Name = "pano", FileName = "pano.png" });
            File.WriteAllBytes(Path.Combine(_projectDirectory, "pano.png"), [1, 2, 3, 4, 5, 6, 7, 8]);
            Assert.Null(PanoramaEnvironmentGenerator.GetOrCreateCubemap(view, AssetId, 256));
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(logger.Entries);
    }
}
