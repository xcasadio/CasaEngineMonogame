using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Textures;

public class Texture : Asset
{
    public static readonly string DefaultTextureName = "defaultTexture";

    private long _texture2dAssetId = IdManager.InvalidId;
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

    protected Texture(GraphicsDevice graphicsDevice) : this()
    {
        GraphicsDevice = graphicsDevice;
    }

    public Texture(long texture2dAssetId, GraphicsDevice graphicsDevice) : this(graphicsDevice)
    {
        _texture2dAssetId = texture2dAssetId;
    }

    public Texture(Texture2D texture2d) : this(texture2d.GraphicsDevice)
    {
        AssetInfo.Name = "Texture";
        Texture2d = texture2d;
        //ScreenSize = new ScreenSize(texture2d.Width, texture2d.Height, new Screen(GraphicsDevice));
    }

    [Obsolete("only for UI")]
    public Texture(string filename, AssetContentManager assetContentManager) : this(assetContentManager.GraphicsDevice)
    {
        AssetInfo.FileName = filename;
    }

    public void Load(AssetContentManager assetContentManager)
    {
        GraphicsDevice = assetContentManager.GraphicsDevice;
        var assetInfo = GameSettings.AssetInfoManager.Get(_texture2dAssetId);
        Texture2d = assetContentManager.Load<Texture2D>(assetInfo);
        Resource.Name = AssetInfo.FileName;
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
            Texture2d = assetContentManager.Load<Texture2D>(AssetInfo);
        }

        GraphicsDevice = device;
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element.GetProperty("asset"), option);

        PreferredSamplerState = element.GetProperty("sampler_state").GetSamplerState();
        _texture2dAssetId = element.GetProperty("texture_asset_id").GetInt32();

        //if (!string.IsNullOrEmpty(AssetInfo.FileName) && File.Exists(AssetInfo.FileName))
        //{
        //    LoadTexture(AssetInfo.FileName);
        //}
    }

#if EDITOR

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        jObject.Add("texture_asset_id", _texture2dAssetId);

        var newNode = new JObject();
        PreferredSamplerState.Save(newNode);
        jObject.Add("sampler_state", newNode);
    }
#endif
}
