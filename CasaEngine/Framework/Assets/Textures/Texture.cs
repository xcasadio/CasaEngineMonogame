
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets.Loaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Textures;

public class Texture : ObjectBase, IAssetable
{
    public static readonly string DefaultTextureName = "defaultTexture";

    private Guid _texture2dAssetId = Guid.Empty;
    protected Texture2D Texture2d;

    // ADR-0037: a texture loaded from its asset shares its Texture2D with every other user of that image, so it
    // holds it through a handle instead of owning it. A texture built on a raw Texture2D owns it.
    private AssetHandle<Texture2D> _texture2dHold;

    // ADR-0124: a texture that asks for mipmaps owns a Texture2D with a full chain, built from the pixels of the
    // shared image, which stays at one level for its other users.
    private Texture2D _mipmappedTexture2d;

    public GraphicsDevice GraphicsDevice { get; private set; }

    public virtual Texture2D Resource
    {
        get =>
            // Textures and render targets have a different treatment because textures could be set,
            // because both are persistent shader parameters, and because they could be created without using content managers.
            // For that reason the nullified resources could be accessed.
            //if (xnaTexture != null && xnaTexture.IsDisposed)
            //xnaTexture = null;
            Texture2d;
        // This is only allowed for videos. 
        // Doing something to avoid this “set” is unnecessary and probably will make more complex some classes just for this special case. 
        // Besides, an internal statement elegantly prevents a bad use of this set.
        // Just don’t dispose this texture because the resource is managed by the video.
        internal set
        {
            Texture2d = value;
        }
    }

    public SamplerState PreferredSamplerState { get; set; } = SamplerState.AnisotropicWrap;

    /// <summary>
    /// True when the texture is drawn with a full mipmap chain (<c>generate_mipmaps</c> of the <c>.texture</c> asset,
    /// ADR-0124). It must be set before <see cref="Load(AssetContentManager)"/>. The texture then owns a copy of the
    /// image with the chain, made at load time and again after a device loss; the shared image is not modified.
    /// False by default.
    /// </summary>
    public bool GenerateMipmaps { get; set; }

    public int Width => Texture2d.Width;

    public int Height => Texture2d.Height;

    public Texture()
    {
    }

    protected Texture(GraphicsDevice graphicsDevice) : this()
    {
        GraphicsDevice = graphicsDevice;
    }

    public Texture(Guid texture2dAssetId, GraphicsDevice graphicsDevice) : this(graphicsDevice)
    {
        _texture2dAssetId = texture2dAssetId;
    }

    public Texture(Texture2D texture2d) : this(texture2d.GraphicsDevice)
    {
        Texture2d = texture2d;
        //ScreenSize = new ScreenSize(texture2d.Width, texture2d.Height, new ScreenGui(GraphicsDevice));
    }

    /// <summary>
    /// Resolves the image of this texture. A shared texture asset is loaded by several users (every sprite of a
    /// sheet calls this), so the image is acquired once and held until <see cref="Dispose"/> (ADR-0037).
    /// </summary>
    public void Load(AssetContentManager assetContentManager)
    {
        GraphicsDevice = assetContentManager.GraphicsDevice;

        if (GenerateMipmaps)
        {
            LoadMipmapped(assetContentManager);
            return;
        }

        _texture2dHold ??= assetContentManager.Acquire<Texture2D>(_texture2dAssetId);
        Texture2d = _texture2dHold.Asset;
        Resource.Name = FileName;
    }

    // Every user of a shared Texture calls Load (materials, meshes, sprites, tile maps): the chain is built once.
    private void LoadMipmapped(AssetContentManager assetContentManager)
    {
        if (_mipmappedTexture2d is { IsDisposed: false })
        {
            Texture2d = _mipmappedTexture2d;
            return;
        }

        using var image = assetContentManager.Acquire<Texture2D>(_texture2dAssetId);
        SetMipmappedTexture(CreateMipmappedTexture(GraphicsDevice, image.Asset));
    }

    private void SetMipmappedTexture(Texture2D mipmapped)
    {
        _mipmappedTexture2d = mipmapped;
        Texture2d = mipmapped;
        Resource.Name = FileName;
    }

    // Copies the level 0 pixels of the image into a new texture that has the whole chain (MipmapChain, ADR-0101).
    private Texture2D CreateMipmappedTexture(GraphicsDevice device, Texture2D image)
    {
        if (image.Format != SurfaceFormat.Color)
        {
            throw new InvalidOperationException(
                $"Cannot generate mipmaps for texture asset '{Name}' (image asset '{_texture2dAssetId}'): the image format is {image.Format}, expected {SurfaceFormat.Color}.");
        }

        var level0Pixels = new Color[image.Width * image.Height];
        image.GetData(level0Pixels);

        var mipmapped = new Texture2D(device, image.Width, image.Height, true, SurfaceFormat.Color);
        try
        {
            MipmapChain.UploadChain(mipmapped, level0Pixels);
        }
        catch
        {
            mipmapped.Dispose();
            throw;
        }

        return mipmapped;
    }

    // The image went with the device. The cache may still hold the disposed instance: the first texture to notice
    // reads a fresh one and replaces it (the holders are kept); the others then find it in the cache.
    private AssetHandle<Texture2D> AcquireCurrentImage(AssetContentManager assetContentManager)
    {
        var current = assetContentManager.Acquire<Texture2D>(_texture2dAssetId);
        if (current.Asset.IsDisposed)
        {
            current.Dispose();
            assetContentManager.Replace(_texture2dAssetId, assetContentManager.LoadCopy<Texture2D>(_texture2dAssetId));
            current = assetContentManager.Acquire<Texture2D>(_texture2dAssetId);
        }

        return current;
    }

    /// <summary>
    /// Gives back the hold on a shared image (ADR-0037), or disposes an image this texture owns: the default
    /// texture built on a raw <see cref="Texture2D"/>, or the copy with a mipmap chain (ADR-0124). The asset
    /// manager calls it when it frees the texture (<see cref="IAssetable"/>).
    /// </summary>
    public void Dispose()
    {
        DisposeManagedResources();
    }

    protected void DisposeManagedResources()
    {
        if (_mipmappedTexture2d != null)
        {
            if (!_mipmappedTexture2d.IsDisposed)
            {
                _mipmappedTexture2d.Dispose();
            }

            _mipmappedTexture2d = null;
            Texture2d = null;
            return;
        }

        if (_texture2dHold != null)
        {
            _texture2dHold.Dispose();
            _texture2dHold = null;
            Texture2d = null;
            return;
        }

        if (Texture2d is { IsDisposed: false })
        {
            Resource?.Dispose();
        }
    }

    public virtual void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        if (Resource == null)
        {
            return;
        }

        if (_mipmappedTexture2d != null)
        {
            // The owned copy went with the device: rebuild its chain from the current image (ADR-0124).
            if (_mipmappedTexture2d.IsDisposed)
            {
                using var image = AcquireCurrentImage(assetContentManager);
                SetMipmappedTexture(CreateMipmappedTexture(device, image.Asset));
            }
        }
        else if (string.IsNullOrEmpty(FileName))
        {
            // Only recreate if actually disposed (e.g. DX9/OpenGL).
            // In DX11, static textures survive a swap-chain reset; recreating here
            // would overwrite valid pixel data with a blank (black) texture.
            if (Texture2d is { IsDisposed: true })
            {
                Texture2d = new Texture2D(device, Texture2d.Width, Texture2d.Height);
            }
        }
        else if (Texture2d is { IsDisposed: true })
        {
            // The shared image went with the device (see AcquireCurrentImage). This texture's hold moves to the
            // current instance.
            var current = AcquireCurrentImage(assetContentManager);

            _texture2dHold?.Dispose();
            _texture2dHold = current;
            Texture2d = current.Asset;
        }

        GraphicsDevice = device;
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        PreferredSamplerState = element["sampler_state"].GetSamplerState();
        _texture2dAssetId = element["texture_asset_id"].GetGuid();
        GenerateMipmaps = element["generate_mipmaps"] is { } generateMipmaps && generateMipmaps.GetBoolean();

        //if (!string.IsNullOrEmpty(AssetInfo.FileName) && File.Exists(AssetInfo.FileName))
        //{
        //    LoadTexture(AssetInfo.FileName);
        //}
    }
}