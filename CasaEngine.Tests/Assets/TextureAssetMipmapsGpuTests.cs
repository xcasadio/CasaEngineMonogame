using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Tests.UI.Backend;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Tests.Assets;

/// <summary>
/// ADR-0124: a <see cref="Texture"/> whose asset sets <c>generate_mipmaps</c> owns a copy of its image with a full chain, and
/// leaves the shared image at one level. The image comes from a loader that makes it from known pixels, so no file is read. Skipped
/// where no device can be created.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class TextureAssetMipmapsGpuTests
{
    // 5 x 3: not a power of two, and a chain of three levels (5 x 3, 2 x 1, 1 x 1).
    private const int ImageWidth = 5;
    private const int ImageHeight = 3;

    private static readonly Guid ImageId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");

    private sealed class PixelImageLoader : IAssetLoader
    {
        public SurfaceFormat Format { get; set; } = SurfaceFormat.Color;

        public int Loads => Images.Count;

        public List<Texture2D> Images { get; } = new();

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var image = new Texture2D(assetContentManager.GraphicsDevice, ImageWidth, ImageHeight, false, Format);
            if (Format == SurfaceFormat.Color)
            {
                image.SetData(ImagePixels());
            }

            Images.Add(image);
            return image;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static Color[] ImagePixels()
    {
        var pixels = new Color[ImageWidth * ImageHeight];
        for (int y = 0; y < ImageHeight; y++)
        {
            for (int x = 0; x < ImageWidth; x++)
            {
                pixels[y * ImageWidth + x] = new Color(x * 40, y * 80, (x + y * ImageWidth) * 10, 255 - x * 10);
            }
        }

        return pixels;
    }

    private static AssetContentManager NewManager(GraphicsDevice device, PixelImageLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [ImageId] = new AssetInfo(ImageId) { Name = "image", FileName = "image.png" },
        };

        var manager = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        manager.Initialize(device);
        manager.RegisterAssetLoader(typeof(Texture2D), loader);
        return manager;
    }

    private static Texture NewTexture(GraphicsDevice device, bool generateMipmaps)
    {
        return new Texture(ImageId, device)
        {
            Name = "mipmap-test-texture",
            FileName = "mipmap-test.texture",
            GenerateMipmaps = generateMipmaps,
        };
    }

    private static Color[] ReadLevel(Texture2D texture, int level)
    {
        int width = MipmapChain.GetLevelSize(texture.Width, level);
        int height = MipmapChain.GetLevelSize(texture.Height, level);
        var pixels = new Color[width * height];
        texture.GetData(level, null, pixels, 0, pixels.Length);
        return pixels;
    }

    [GpuFact]
    public void WithoutTheField_TheTextureIsTheSharedImage_AtOneLevel()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var loader = new PixelImageLoader();
            var manager = NewManager(GpuDeviceHost.Instance.GraphicsDevice, loader);
            var texture = NewTexture(GpuDeviceHost.Instance.GraphicsDevice, generateMipmaps: false);

            texture.Load(manager);

            using var image = manager.Acquire<Texture2D>(ImageId);
            Assert.Same(image.Asset, texture.Resource);
            Assert.Equal(1, texture.Resource.LevelCount);
            Assert.Equal(1, loader.Loads);

            texture.Dispose();
            manager.CollectUnreferenced();
        });
    }

    [GpuFact]
    public void WithTheField_TheTextureOwnsTheFullChain_AndTheSharedImageStaysAtOneLevel()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var loader = new PixelImageLoader();
            var manager = NewManager(GpuDeviceHost.Instance.GraphicsDevice, loader);
            var texture = NewTexture(GpuDeviceHost.Instance.GraphicsDevice, generateMipmaps: true);

            texture.Load(manager);

            Color[] level0 = ImagePixels();
            Assert.Equal(MipmapChain.GetLevelCount(ImageWidth, ImageHeight), texture.Resource.LevelCount);
            Assert.Equal(3, texture.Resource.LevelCount);
            Assert.Equal(ImageWidth, texture.Width);
            Assert.Equal(ImageHeight, texture.Height);
            Assert.Equal(level0, ReadLevel(texture.Resource, 0));
            Assert.Equal(MipmapChain.ComputeNextLevel(level0, ImageWidth, ImageHeight), ReadLevel(texture.Resource, 1));

            // The shared image is untouched: one level, still the same pixels, and not the texture of the asset.
            using var image = manager.Acquire<Texture2D>(ImageId);
            Assert.NotSame(image.Asset, texture.Resource);
            Assert.Equal(1, image.Asset.LevelCount);
            Assert.Equal(level0, ReadLevel(image.Asset, 0));
            Assert.Equal(1, loader.Loads);

            texture.Dispose();
            image.Dispose();
            manager.CollectUnreferenced();
        });
    }

    [GpuFact]
    public void LoadingTwice_KeepsTheSameChain_AndTheSharedImageIsNoLongerHeld()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var loader = new PixelImageLoader();
            var manager = NewManager(GpuDeviceHost.Instance.GraphicsDevice, loader);
            var texture = NewTexture(GpuDeviceHost.Instance.GraphicsDevice, generateMipmaps: true);

            texture.Load(manager);
            Texture2D first = texture.Resource;
            texture.Load(manager);

            Assert.Same(first, texture.Resource);
            Assert.False(first.IsDisposed);
            Assert.Equal(1, loader.Loads);

            // Nobody holds the shared image once the chain is built: a collection frees it, and not the chain.
            Assert.Equal(1, manager.CollectUnreferenced());
            Assert.True(loader.Images[0].IsDisposed);
            Assert.False(first.IsDisposed);

            texture.Dispose();
        });
    }

    [GpuFact]
    public void Dispose_FreesTheOwnedChain()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var loader = new PixelImageLoader();
            var manager = NewManager(GpuDeviceHost.Instance.GraphicsDevice, loader);
            var texture = NewTexture(GpuDeviceHost.Instance.GraphicsDevice, generateMipmaps: true);
            texture.Load(manager);
            Texture2D owned = texture.Resource;

            texture.Dispose();

            Assert.True(owned.IsDisposed);
            Assert.Null(texture.Resource);

            manager.CollectUnreferenced();
        });
    }

    [GpuFact]
    public void AfterADeviceLoss_TheChainIsBuiltAgainFromTheImage_AndTheImageIsNotHeld()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            var loader = new PixelImageLoader();
            var manager = NewManager(device, loader);
            var texture = NewTexture(device, generateMipmaps: true);
            texture.Load(manager);
            Texture2D lost = texture.Resource;

            // The device loss: the owned texture goes, the shared image survived.
            lost.Dispose();
            texture.OnDeviceReset(device, manager);

            Assert.NotSame(lost, texture.Resource);
            Assert.False(texture.Resource.IsDisposed);
            Assert.Equal(MipmapChain.GetLevelCount(ImageWidth, ImageHeight), texture.Resource.LevelCount);
            Color[] level0 = ImagePixels();
            Assert.Equal(level0, ReadLevel(texture.Resource, 0));
            Assert.Equal(MipmapChain.ComputeNextLevel(level0, ImageWidth, ImageHeight), ReadLevel(texture.Resource, 1));
            Assert.Equal(1, loader.Loads);

            // The shared image was acquired for the copy and given back.
            Assert.Equal(1, manager.CollectUnreferenced());
            Assert.True(loader.Images[0].IsDisposed);
            Assert.False(texture.Resource.IsDisposed);

            texture.Dispose();
        });
    }

    [GpuFact]
    public void AfterADeviceLoss_TheImageThatWentWithTheDeviceIsReadAgain()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            var loader = new PixelImageLoader();
            var manager = NewManager(device, loader);
            var texture = NewTexture(device, generateMipmaps: true);
            texture.Load(manager);

            // Both the owned chain and the cached image are gone.
            texture.Resource.Dispose();
            loader.Images[0].Dispose();
            texture.OnDeviceReset(device, manager);

            Assert.Equal(2, loader.Loads);
            Assert.False(texture.Resource.IsDisposed);
            Assert.Equal(MipmapChain.GetLevelCount(ImageWidth, ImageHeight), texture.Resource.LevelCount);
            Assert.Equal(ImagePixels(), ReadLevel(texture.Resource, 0));

            Assert.Equal(1, manager.CollectUnreferenced());
            Assert.True(loader.Images[1].IsDisposed);

            texture.Dispose();
        });
    }

    [GpuFact]
    public void AnImageThatIsNotColor_IsReportedWithTheNameOfTheAsset()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            var loader = new PixelImageLoader { Format = SurfaceFormat.Bgr565 };
            var manager = NewManager(GpuDeviceHost.Instance.GraphicsDevice, loader);
            var texture = NewTexture(GpuDeviceHost.Instance.GraphicsDevice, generateMipmaps: true);

            var error = Assert.Throws<InvalidOperationException>(() => texture.Load(manager));

            Assert.Contains("mipmap-test-texture", error.Message);
            Assert.Contains(ImageId.ToString(), error.Message);
            Assert.Contains(nameof(SurfaceFormat.Bgr565), error.Message);

            manager.CollectUnreferenced();
        });
    }
}
