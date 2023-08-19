using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Textures;

public class Texture : Asset
{
    public static readonly string DefaultTextureName = "defaultTexture";

    protected Texture2D? Texture2d;
    private SamplerState _preferedSamplerState = SamplerState.AnisotropicWrap;

    public GraphicsDevice GraphicsDevice { get; private set; }

    public virtual Texture2D? Resource
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
            //ScreenSize = value == null ? new ScreenSize(0, 0, new Screen(GraphicsDevice)) : new ScreenSize(Texture2d.Width, Texture2d.Height, new Screen(GraphicsDevice));
        }
    }

    public SamplerState PreferredSamplerState
    {
        get => _preferedSamplerState;
        set => _preferedSamplerState = value;
    }

    public int Width => Texture2d.Width;

    public int Height => Texture2d.Height;

    //public ScreenSize ScreenSize { get; protected set; }

    public Texture()
    {
        Name = "Empty Texture";
    }

    public Texture(GraphicsDevice graphicsDevice) : this()
    {
        GraphicsDevice = graphicsDevice;
    }

    public Texture(Texture2D texture2d) : this(texture2d.GraphicsDevice)
    {
        Name = "Texture";
        Texture2d = texture2d;
        //ScreenSize = new ScreenSize(texture2d.Width, texture2d.Height, new Screen(GraphicsDevice));
    }


    public Texture(GraphicsDevice graphicsDevice, string filename, AssetContentManager assetContentManager) : this(graphicsDevice)
    {
        FileName = filename;
        Initialize(graphicsDevice, assetContentManager);
    }

    public void Initialize(GraphicsDevice graphicsDevice, AssetContentManager assetContentManager)
    {
        GraphicsDevice = graphicsDevice;
        LoadTexture(FileName, assetContentManager);
    }

    private void LoadTexture(string filename, AssetContentManager assetContentManager)
    {
        Name = Path.GetFileNameWithoutExtension(filename);
        FileName = filename;

        if (File.Exists(filename) == false)
        {
            //TODO : all asset must be loaded from  ProjectPath
            filename = Path.Combine(GameSettings.ProjectSettings.ProjectPath, filename);
            if (File.Exists(filename) == false)
            {
                throw new ArgumentException("Failed to load texture: File " + FileName + " does not exists!", nameof(filename));
            }
        }

        try
        {
            Texture2d = assetContentManager.Load<Texture2D>(filename, GraphicsDevice);
            //ScreenSize = new ScreenSize(Texture2d.Width, Texture2d.Height, new Screen(GraphicsDevice));
            Resource.Name = FileName;
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to load texture: " + filename, e);
        }
    }

    protected override void DisposeManagedResources()
    {
        base.DisposeManagedResources();
        if (Texture2d is { IsDisposed: false })
        {
            Resource?.Dispose();
        }
    }

    internal override void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        if (Resource == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(FileName))
        {
            Texture2d = new Texture2D(device, Texture2d.Width, Texture2d.Height);
        }
        else if (Texture2d is { IsDisposed: true })
        {
            Texture2d = assetContentManager.Load<Texture2D>(FileName, device);
        }

        GraphicsDevice = device;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element.GetProperty("asset"), option);

        //if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName))
        //{
        //    LoadTexture(FileName);
        //}
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //jObject.Add("preferredSamplerState", PreferredSamplerState);
        //
        //PreferredSamplerState.Filter = TextureFilter.Linear;
        //PreferredSamplerState.AddressU = TextureAddressMode.Wrap;
        //PreferredSamplerState.AddressV = TextureAddressMode.Wrap;
        //PreferredSamplerState.AddressW = TextureAddressMode.Wrap;
        //PreferredSamplerState.BorderColor = Color.White;
        //PreferredSamplerState.MaxAnisotropy = 4;
        //PreferredSamplerState.MaxMipLevel = 0;
        //PreferredSamplerState.MipMapLevelOfDetailBias = 0.0f;
        //PreferredSamplerState.ComparisonFunction = CompareFunction.Never;
        //PreferredSamplerState.FilterMode = TextureFilterMode.Default;
    }
#endif
}