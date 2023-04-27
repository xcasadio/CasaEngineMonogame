using System.Text.Json;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Screen = CasaEngine.Framework.UserInterface.Screen;
using Size = CasaEngine.Core.Helpers.Size;

namespace CasaEngine.Framework.Assets.Textures;

public class Texture : Asset
{
    protected Texture2D? XnaTexture;
    private SamplerState _preferedSamplerState = SamplerState.AnisotropicWrap;
    //private static Texture _blackTexture, _greyTexture, _whiteTexture;

    public GraphicsDevice GraphicsDevice { get; private set; }

    public virtual Texture2D? Resource
    {
        get =>
            // Textures and render targets have a different treatment because textures could be set,
            // because both are persistent shader parameters, and because they could be created without using content managers.
            // For that reason the nullified resources could be accessed.
            //if (xnaTexture != null && xnaTexture.IsDisposed)
            //xnaTexture = null;
            XnaTexture;
        // This is only allowed for videos. 
        // Doing something to avoid this “set” is unnecessary and probably will make more complex some classes just for this special case. 
        // Besides, an internal statement elegantly prevents a bad use of this set.
        // Just don’t dispose this texture because the resource is managed by the video.
        internal set
        {
            XnaTexture = value;
            Size = value == null ?
                new Size(0, 0, new Screen(GraphicsDevice)) : new Size(XnaTexture.Width, XnaTexture.Height, new Screen(GraphicsDevice));
        }
    }

    public SamplerState PreferredSamplerState
    {
        get => _preferedSamplerState;
        set => _preferedSamplerState = value;
    }

    public int Width => Size.Width;

    public int Height => Size.Height;

    public Size Size { get; protected set; }

    public Texture()
    {
    }

    public Texture(GraphicsDevice graphicsDevice)
    {
        GraphicsDevice = graphicsDevice;
        Name = "Empty Texture";
    }

    public Texture(Texture2D xnaTexture) : this(xnaTexture.GraphicsDevice)
    {
        Name = "Texture";
        XnaTexture = xnaTexture;
        Size = new Size(xnaTexture.Width, xnaTexture.Height, new Screen(GraphicsDevice));
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
            filename = Path.Combine(GameSettings.ProjectManager.ProjectPath, filename);
            if (File.Exists(filename) == false)
            {
                throw new ArgumentException("Failed to load texture: File " + FileName + " does not exists!", nameof(filename));
            }
        }

        try
        {
            XnaTexture = assetContentManager.Load<Texture2D>(filename, GraphicsDevice);
            Size = new Size(XnaTexture.Width, XnaTexture.Height, new Screen(GraphicsDevice));
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
        if (XnaTexture is { IsDisposed: false })
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
            XnaTexture = new Texture2D(device, Size.Width, Size.Height);
        }
        else if (XnaTexture is { IsDisposed: true })
        {
            XnaTexture = assetContentManager.Load<Texture2D>(FileName, device);
        }

        GraphicsDevice = device;
    }

    public override void Load(BinaryReader br, SaveOption option)
    {

    }

    public override void Load(XmlElement el, SaveOption option)
    {

    }

    public override void Load(JsonElement element)
    {
        base.Load(element.GetProperty("asset"));

        //if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName))
        //{
        //    LoadTexture(FileName);
        //}
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);

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