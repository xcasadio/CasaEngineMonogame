using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Video;

/// <summary>
/// What the world video component queues for its draw order (ADR-0092): the entry carries the order, and an order of 0
/// leaves the entry as before. The asset catalogue is global state: these tests share the single-threaded GPU collection.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public sealed class VideoSpriteComponentDrawOrderGpuTests : IDisposable
{
    private static readonly Guid VideoId = Guid.Parse("d3000000-0000-4000-8000-000000000001");
    private const string VideoName = "order-film";

    private readonly List<string> _files = new();

    public VideoSpriteComponentDrawOrderGpuTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(VideoId) { Name = VideoName, FileName = "order-film.mdecvideo", AssetType = "mdecvideo" });
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

    private static Entity VideoEntity(int drawOrder, float z)
    {
        var component = new VideoSpriteComponent { VideoName = VideoName, LocalPosition = new Vector3(0f, 0f, z), DrawOrder = drawOrder };
        return new Entity { Name = "Film", RootComponent = component };
    }

    [GpuTheory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-8)]
    public void Draw_QueuesAnEntryThatCarriesTheDrawOrder_WithoutKeyAndIgnoringDepth(int drawOrder)
    {
        var path = WorldVideoTestFiles.Write(3, 10, 1);
        _files.Add(path);
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var harness = new WorldVideoHarness(new WorldVideoTestFiles.Loader(path), GpuDeviceHost.Instance.GraphicsDevice);
            var entity = VideoEntity(drawOrder, 3f);
            harness.AddToWorld(entity);
            entity.IsVisible = true;

            entity.Draw(0f);

            var entry = Assert.Single(harness.Queue)!;
            Assert.Equal(drawOrder, (int)WorldVideoHarness.Field(entry, "DrawOrder"));
            Assert.False((bool)WorldVideoHarness.Field(entry, "HasSortKey"));
            Assert.True((bool)WorldVideoHarness.Field(entry, "IgnoresDepth"));
        });
    }
}
