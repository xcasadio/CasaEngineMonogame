using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Video;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Video;

/// <summary>
/// The world video component (ADR-0091) with the real player: it plays, advances, ends and keeps its last picture; what it
/// queues on the renderer; the release of the player and of the asset hold; the creation failures that need a device; the
/// world loading path; the absence of allocation in <c>Update</c>; and the pixels of a sprite of a smaller Z blended over the
/// film. The asset catalogue is global state: these tests share the single-threaded GPU collection.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public sealed class VideoSpriteComponentGpuTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("d2000000-0000-4000-8000-000000000001");
    private const string VideoName = "gpu-film";

    private readonly List<string> _files = new();

    public VideoSpriteComponentGpuTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = VideoName, FileName = "gpu-film.mdecvideo", AssetType = "mdecvideo" });
    }

    public void Dispose()
    {
        AssetCatalog.ClearInternal();
        foreach (var file in _files)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A test that keeps its player open leaves the temporary file to the system.
            }
        }
    }

    private string NewVideo(int frameCount, uint rate, int corruptFrame = -1)
    {
        var path = WorldVideoTestFiles.Write(frameCount, rate, 1, corruptFrame);
        _files.Add(path);
        return path;
    }

    private static Entity VideoEntity(Vector3 position)
    {
        var component = new VideoSpriteComponent { VideoName = VideoName, LocalPosition = position };
        return new Entity { Name = "Film", RootComponent = component };
    }

    private static VideoSpriteComponent Video(Entity entity) => (VideoSpriteComponent)entity.RootComponent!;

    /// <summary>Loads an entity from its JSON, with the given <c>is_visible</c> (none when null).</summary>
    private static Entity LoadedVideoEntity(bool? isVisible)
    {
        var node = new JObject();
        EditorEntityJsonSerializer.SaveEntity(VideoEntity(Vector3.Zero), node);
        if (isVisible.HasValue)
        {
            node["is_visible"] = isVisible.Value;
        }

        var entity = new Entity();
        entity.Load(node);
        return entity;
    }

    private static byte[] ReadTexture(Texture2D texture)
    {
        var pixels = new byte[texture.Width * texture.Height * 4];
        texture.GetData(pixels);
        return pixels;
    }

    // ---- Creation and release ----

    [GpuTheory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void InitializeWithWorld_CreatesThePlayer_HidesTheEntity_AndDetachReleasesEverything(bool? isVisible)
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = LoadedVideoEntity(isVisible);

            harness.AddToWorld(entity);

            var component = Video(entity);
            Assert.True(component.HasPlayer);
            Assert.False(entity.IsVisible);
            Assert.False(component.IsPlaying);
            Assert.False(component.IsEnded);
            Assert.Equal(0, component.CurrentFrameIndex);
            Assert.Equal(0, harness.Manager.CollectUnreferenced());

            // A hidden entity draws nothing.
            entity.Draw(0f);
            Assert.Empty(harness.Queue);

            component.Detach();

            Assert.False(component.HasPlayer);
            Assert.Equal(1, harness.Manager.CollectUnreferenced());
            File.Delete(path);
            Assert.False(File.Exists(path));
        });
    }

    [GpuFact]
    public void Dispose_ReleasesThePlayerAndTheHold_AndSecondInitializeWithWorldReleasesTheFirst()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(Vector3.Zero);
            harness.AddToWorld(entity);
            var component = Video(entity);

            // The second initialization drops the first player: the asset is still held once, never twice.
            entity.InitializeWithWorld(harness.World);
            Assert.True(component.HasPlayer);
            Assert.Equal(0, harness.Manager.CollectUnreferenced());

            component.Dispose();
            component.Dispose();

            Assert.False(component.HasPlayer);
            Assert.Equal(1, harness.Manager.CollectUnreferenced());
        });
    }

    [GpuFact]
    public void Clone_CreatesItsOwnPlayer()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var original = VideoEntity(Vector3.Zero);
            var clone = original.Clone();

            harness.AddToWorld(original);
            harness.AddToWorld(clone);
            Video(original).Play();
            Video(original).Update(0.1f);

            Assert.Equal(1, Video(original).CurrentFrameIndex);
            Assert.Equal(0, Video(clone).CurrentFrameIndex);
            Assert.True(Video(clone).HasPlayer);
        });
    }

    // ---- Failures that need a device ----

    private sealed class DeletingLoader : IAssetLoader
    {
        private readonly string _path;

        public DeletingLoader(string path) => _path = path;

        /// <summary>Reads the header, then removes the file, so that the player cannot open it.</summary>
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var asset = MdecVideoAsset.Load(_path);
            File.Delete(_path);
            return asset;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    [GpuFact]
    public void InitializeWithWorld_WhenThePlayerCannotOpenTheFile_ThrowsWithTheOriginalException_AndReleasesTheHold()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(null, GpuDeviceHost.Instance.GraphicsDevice);
            harness.Manager.RegisterAssetLoader(typeof(MdecVideoAsset), new DeletingLoader(path));
            var entity = VideoEntity(Vector3.Zero);

            var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

            Assert.Contains("VideoSpriteComponent", error.Message);
            Assert.Contains("'video'", error.Message);
            Assert.Contains(VideoName, error.Message);
            Assert.IsAssignableFrom<IOException>(error.InnerException);
            Assert.False(Video(entity).HasPlayer);
            Assert.Equal(1, harness.Manager.CollectUnreferenced());
        });
    }

    [GpuFact]
    public void InitializeWithWorld_WhenTheFirstPictureCannotBeDecoded_ThrowsDisposesThePlayer_AndReleasesTheHold()
    {
        var path = NewVideo(3, 10, corruptFrame: 0);
        var errors = new List<string>();
        Logs.AddLogger(new CapturingLogger(errors));
        try
        {
            GpuDeviceHost.Instance.Invoke(() =>
            {
                var device = GpuDeviceHost.Instance.GraphicsDevice;
                var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), device);
                var entity = VideoEntity(Vector3.Zero);
                var before = LiveTextureCount(device);

                var error = Assert.Throws<InvalidDataException>(() => harness.AddToWorld(entity));

                Assert.Contains("'video'", error.Message);
                Assert.Contains(VideoName, error.Message);
                Assert.Contains("first picture", error.Message);
                Assert.Null(error.InnerException);
                Assert.False(Video(entity).HasPlayer);
                Assert.Equal(before, LiveTextureCount(device));
                Assert.Equal(1, harness.Manager.CollectUnreferenced());
                File.Delete(path);
                Assert.False(File.Exists(path));
            });
        }
        finally
        {
            Logs.Close();
        }

        Assert.Single(errors, message => message.Contains("frame 0"));
    }

    [GpuFact]
    public void WorldLoad_OfAnEntityWhoseFilmCannotBePlayed_LogsTheError_DoesNotAddTheEntity_AndReleasesTheHold()
    {
        var path = NewVideo(3, 10);
        var errors = new List<string>();
        Logs.AddLogger(new CapturingLogger(errors));
        try
        {
            GpuDeviceHost.Instance.Invoke(() =>
            {
                var harness = new WorldVideoHarness(null, GpuDeviceHost.Instance.GraphicsDevice);
                harness.Manager.RegisterAssetLoader(typeof(MdecVideoAsset), new DeletingLoader(path));
                var entity = VideoEntity(Vector3.Zero);

                harness.World.AddEntity(entity);
                harness.World.Update(1f / 60f);

                Assert.DoesNotContain(entity, harness.World.Entities);
                Assert.Equal(1, harness.Manager.CollectUnreferenced());
            });
        }
        finally
        {
            Logs.Close();
        }

        var logged = Assert.Single(errors, message => message.Contains(VideoName));
        Assert.Contains("VideoSpriteComponent", logged);
    }

    // ---- Playback ----

    [GpuFact]
    public void Update_AdvancesTheFilmOnlyWhilePlaying_EndsIt_AndKeepsTheLastPictureDrawn()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(Vector3.Zero);
            harness.AddToWorld(entity);
            var component = Video(entity);

            component.Update(1f);
            Assert.Equal(0, component.CurrentFrameIndex);
            Assert.False(component.IsPlaying);

            component.Play();
            Assert.True(component.IsPlaying);
            component.Update(0.1f);
            Assert.Equal(1, component.CurrentFrameIndex);

            component.Stop();
            Assert.False(component.IsPlaying);
            component.Update(0.1f);
            Assert.Equal(1, component.CurrentFrameIndex);
            Assert.False(component.IsEnded);

            component.Play();
            Assert.Equal(0, component.CurrentFrameIndex);
            component.Update(0.1f);
            component.Update(0.1f);
            Assert.True(component.IsPlaying);
            component.Update(0.1f);
            Assert.True(component.IsEnded);
            Assert.False(component.IsPlaying);
            Assert.Equal(2, component.CurrentFrameIndex);

            // Further updates change nothing; the last picture is still drawn while the entity is visible.
            component.Update(1f);
            Assert.Equal(2, component.CurrentFrameIndex);
            entity.IsVisible = true;
            entity.Draw(0f);
            Assert.Single(harness.Queue);

            // Play again restarts from the first picture.
            component.Play();
            Assert.False(component.IsEnded);
            Assert.True(component.IsPlaying);
            Assert.Equal(0, component.CurrentFrameIndex);
        });
    }

    [GpuFact]
    public void Update_WithAnInvalidElapsedTime_AdvancesNothing()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(Vector3.Zero);
            harness.AddToWorld(entity);
            var component = Video(entity);
            component.Play();

            component.Update(0f);
            component.Update(-1f);
            component.Update(float.NaN);

            Assert.Equal(0, component.CurrentFrameIndex);
            Assert.True(component.IsPlaying);
        });
    }

    [GpuFact]
    public void Update_AllocatesNothing_WhilePlaying()
    {
        var path = NewVideo(40, 30);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(Vector3.Zero);
            harness.AddToWorld(entity);
            var component = Video(entity);
            component.Play();
            const float step = 1f / 30f;
            component.Update(step);
            component.Update(step);

            var before = AllocationWindow.Start();
            for (var i = 0; i < 30; i++)
            {
                component.Update(step);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(component.CurrentFrameIndex >= 25, "the loop must decode new frames");
            Assert.Equal(0, allocated);
        });
    }

    // ---- What the renderer receives ----

    [GpuFact]
    public void Draw_QueuesTheWholeTextureWithoutKey_IgnoringDepth_CentredOnThePositionAtItsZ()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(new Vector3(5f, -7f, 3f));
            Video(entity).Color = new Color(10, 20, 30, 40);
            harness.AddToWorld(entity);
            entity.IsVisible = true;

            entity.Draw(0f);

            var entry = Assert.Single(harness.Queue)!;
            Assert.False((bool)WorldVideoHarness.Field(entry, "HasSortKey"));
            Assert.True((bool)WorldVideoHarness.Field(entry, "IgnoresDepth"));
            Assert.Equal(SpriteBlendMode.Opaque, (SpriteBlendMode)WorldVideoHarness.Field(entry, "BlendMode"));
            Assert.Equal(new Color(10, 20, 30, 40), (Color)WorldVideoHarness.Field(entry, "Color"));
            var texture = (Texture2D)WorldVideoHarness.Field(entry, "Texture");
            Assert.Equal(WorldVideoTestFiles.Size, texture.Width);
            Assert.Equal(WorldVideoTestFiles.Size, texture.Height);
            var world = (Matrix)WorldVideoHarness.Field(entry, "WorldMatrix");
            Assert.Equal(new Vector3(5f, -7f, 3f), world.Translation);
            // The quad is the size of the picture.
            Assert.Equal(WorldVideoTestFiles.Size, world.M11);
            Assert.Equal(WorldVideoTestFiles.Size, world.M22);
        });
    }

    [GpuFact]
    public void BoundingBox_CoversThePictureAroundThePosition()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(new Vector3(100f, 50f, 2f));
            harness.AddToWorld(entity);

            var box = Video(entity).GetBoundingBox();

            Assert.Equal(92f, box.Min.X, 3);
            Assert.Equal(108f, box.Max.X, 3);
            Assert.Equal(42f, box.Min.Y, 3);
            Assert.Equal(58f, box.Max.Y, 3);
        });
    }

    // ---- Pixels ----

    private const int TargetSize = 64;

    private static Sprite CreateSprite(Texture2D texture, int size)
    {
        var sprite = (Sprite)RuntimeHelpers.GetUninitializedObject(typeof(Sprite));
        SetBackingField(sprite, nameof(Sprite.Texture), new CasaEngine.Framework.Assets.Textures.Texture { Resource = texture });
        SetBackingField(sprite, nameof(Sprite.SpriteData), new SpriteData
        {
            PositionInTexture = new Rectangle(0, 0, size, size),
            Origin = new Point(size / 2, size / 2),
        });
        return sprite;
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(instance, value);
    }

    private static Color PixelAt(byte[] pixels, int x, int y)
    {
        var index = (y * TargetSize + x) * 4;
        return new Color(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
    }

    private static void AssertClose(Color expected, Color actual, string what)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= 1 && Math.Abs(expected.G - actual.G) <= 1 && Math.Abs(expected.B - actual.B) <= 1,
            $"{what}: expected {expected}, got {actual}");
    }

    private static Color Saturate(Color a, Color b) => new(
        (byte)Math.Min(255, a.R + b.R), (byte)Math.Min(255, a.G + b.G), (byte)Math.Min(255, a.B + b.B), (byte)255);

    /// <summary>Draws the scene into a target, reads it back and returns the pixels, with the film drawn by the component.</summary>
    private static byte[] RenderScene(WorldVideoHarness harness, Entity filmEntity, Sprite additiveSprite, float spriteZ,
        bool filmThroughDepth, out Color filmColor)
    {
        var device = GpuDeviceHost.Instance.GraphicsDevice;
        using var target = new RenderTarget2D(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        var previousTargets = device.GetRenderTargets();
        device.SetRenderTarget(target);
        device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);

        if (filmThroughDepth)
        {
            // Control: the same film drawn by the overload that tests and writes the depth.
            filmEntity.IsVisible = false;
            var texture = Video(filmEntity).Texture;
            harness.Renderer.DrawSprite(texture, texture.Bounds, new Point(texture.Width / 2, texture.Height / 2), Vector2.Zero, 0f,
                Vector2.One, Color.White, Video(filmEntity).Position.Z, SpriteEffects.None);
        }
        else
        {
            filmEntity.IsVisible = true;
            filmEntity.Draw(0f);
        }

        var filmEntry = harness.Queue[0]!;
        var film = ReadTexture((Texture2D)WorldVideoHarness.Field(filmEntry, "Texture"));
        filmColor = new Color(film[0], film[1], film[2], (byte)255);

        harness.Renderer.DrawSprite(additiveSprite, Vector2.Zero, 0f, Vector2.One, Color.White, spriteZ, SpriteBlendMode.Additive);

        var view = Matrix.CreateLookAt(new Vector3(0f, 0f, 10f), Vector3.Zero, Vector3.Up);
        var projection = Matrix.CreateOrthographic(TargetSize, TargetSize, 0.1f, 100f);
        harness.Renderer.Flush(new RenderFrame(view, projection, new Vector3(0f, 0f, 10f), new Rectangle(0, 0, TargetSize, TargetSize)));

        device.SetRenderTargets(previousTargets);
        var pixels = new byte[TargetSize * TargetSize * 4];
        target.GetData(pixels);
        return pixels;
    }

    [GpuFact]
    public void ASpriteOfASmallerZ_DrawnAfterTheFilm_BlendsOverIt_WhereAFilmThatWritesDepthWouldHideIt()
    {
        var path = NewVideo(3, 10);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var device = GpuDeviceHost.Instance.GraphicsDevice;
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), device);
            harness.PrepareRendering(GpuDeviceHost.Instance.Game);
            var entity = VideoEntity(new Vector3(0f, 0f, 1f));
            harness.AddToWorld(entity);

            var spriteColor = new Color(30, 40, 50, 255);
            using var spriteTexture = new Texture2D(device, 8, 8, false, SurfaceFormat.Color);
            var spriteData = new Color[64];
            Array.Fill(spriteData, spriteColor);
            spriteTexture.SetData(spriteData);
            var sprite = CreateSprite(spriteTexture, 8);

            // The film is 16 x 16 and the sprite 8 x 8, both centred on the target's centre; pixel (32, 32) is under both.
            var pixels = RenderScene(harness, entity, sprite, spriteZ: 0f, filmThroughDepth: false, out var film);

            AssertClose(Color.Black, PixelAt(pixels, 2, 2), "outside the film");
            AssertClose(film, PixelAt(pixels, 38, 32), "film alone");
            AssertClose(Saturate(film, spriteColor), PixelAt(pixels, 32, 32), "film plus sprite");
            Assert.NotEqual(film, Saturate(film, spriteColor));

            // Control: a film that writes the depth at a larger Z rejects the sprite of the smaller Z.
            var control = RenderScene(harness, entity, sprite, spriteZ: 0f, filmThroughDepth: true, out var controlFilm);
            AssertClose(controlFilm, PixelAt(control, 32, 32), "control: the sprite is rejected by the depth test");
        });
    }

    // ---- Helpers ----

    private static int LiveTextureCount(GraphicsDevice device)
    {
        var field = typeof(GraphicsDevice).GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var resources = (System.Collections.IEnumerable)field.GetValue(device)!;
        var count = 0;
        lock (resources)
        {
            foreach (var entry in resources)
            {
                if (((WeakReference)entry).Target is Texture2D { IsDisposed: false })
                {
                    count++;
                }
            }
        }

        return count;
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _errors;

        public CapturingLogger(List<string> errors) => _errors = errors;

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => _errors.Add(msg);
    }
}
