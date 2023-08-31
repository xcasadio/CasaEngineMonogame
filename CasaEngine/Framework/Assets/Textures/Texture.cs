using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Engine;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Textures;

public class Texture : Asset
{
    public static readonly string DefaultTextureName = "defaultTexture";

    protected Texture2D? Texture2d;

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

    public SamplerState PreferredSamplerState { get; set; } = SamplerState.AnisotropicWrap;

    public int Width => Texture2d.Width;

    public int Height => Texture2d.Height;

    public Texture()
    {
        AssetInfo.Name = "Empty Texture";
    }

    public Texture(GraphicsDevice graphicsDevice) : this()
    {
        GraphicsDevice = graphicsDevice;
    }

    public Texture(Texture2D texture2d) : this(texture2d.GraphicsDevice)
    {
        AssetInfo.Name = "Texture";
        Texture2d = texture2d;
        //ScreenSize = new ScreenSize(texture2d.Width, texture2d.Height, new Screen(GraphicsDevice));
    }


    public Texture(string filename, AssetContentManager assetContentManager) : this(assetContentManager.GraphicsDevice)
    {
        AssetInfo.FileName = filename;
        Initialize(assetContentManager);
    }

    public void Initialize(AssetContentManager assetContentManager)
    {
        GraphicsDevice = assetContentManager.GraphicsDevice;
        LoadTexture(AssetInfo.FileName, assetContentManager);
    }

    private void LoadTexture(string filename, AssetContentManager assetContentManager)
    {
        AssetInfo.Name = Path.GetFileNameWithoutExtension(filename);
        AssetInfo.FileName = filename;

        if (File.Exists(filename) == false)
        {
            //TODO : all asset must be loaded from  ProjectPath
            filename = Path.Combine(EngineEnvironment.ProjectPath, filename);
            if (File.Exists(filename) == false)
            {
                throw new ArgumentException($"Failed to load texture: File {AssetInfo.FileName} does not exists!", nameof(filename));
            }
        }

        try
        {
            var assetInfo = GameSettings.AssetInfoManager.GetOrAdd(filename);
            Texture2d = assetContentManager.Load<Texture2D>(assetInfo, GraphicsDevice);
            //ScreenSize = new ScreenSize(Texture2d.Width, Texture2d.Height, new Screen(GraphicsDevice));
            Resource.Name = AssetInfo.FileName;
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Content Manager: Content manager disposed");
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to load texture: {filename}", e);
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

        if (string.IsNullOrEmpty(AssetInfo.FileName))
        {
            Texture2d = new Texture2D(device, Texture2d.Width, Texture2d.Height);
        }
        else if (Texture2d is { IsDisposed: true })
        {
            Texture2d = assetContentManager.Load<Texture2D>(AssetInfo, device);
        }

        GraphicsDevice = device;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element.GetProperty("asset"), option);

        PreferredSamplerState = element.GetProperty("sampler_state").GetSamplerState();

        //if (!string.IsNullOrEmpty(AssetInfo.FileName) && File.Exists(AssetInfo.FileName))
        //{
        //    LoadTexture(AssetInfo.FileName);
        //}
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        var newNode = new JObject();
        PreferredSamplerState.Save(newNode);
        jObject.Add("sampler_state", newNode);
    }
#endif
}
