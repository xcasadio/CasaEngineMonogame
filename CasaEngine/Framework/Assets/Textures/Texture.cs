using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Textures;

public class Texture : UObject, IAssetable
{
    public static readonly string DefaultTextureName = "defaultTexture";

    private Guid _texture2dAssetId = Guid.Empty;
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
        }
    }

    public SamplerState PreferredSamplerState { get; set; } = SamplerState.AnisotropicWrap;

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

    [Obsolete("only for UI")]
    public Texture(string filename, AssetContentManager assetContentManager) : this(assetContentManager.GraphicsDevice)
    {
        FileName = filename;
    }

    public void Load(AssetContentManager assetContentManager)
    {
        GraphicsDevice = assetContentManager.GraphicsDevice;
        Texture2d = assetContentManager.Load<Texture2D>(_texture2dAssetId);
        Resource.Name = FileName;
    }

    public void Dispose()
    {
        DisposeManagedResources();
    }

    protected void DisposeManagedResources()
    {
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

        if (string.IsNullOrEmpty(FileName))
        {
            Texture2d = new Texture2D(device, Texture2d.Width, Texture2d.Height);
        }
        else if (Texture2d is { IsDisposed: true })
        {
            Texture2d = assetContentManager.Load<Texture2D>(_texture2dAssetId);
        }

        GraphicsDevice = device;
    }

    public override void Load(JsonElement element)
    {
        base.Load(element.GetProperty("asset"));

        PreferredSamplerState = element.GetProperty("sampler_state").GetSamplerState();
        //TODO : remove
        if (element.GetProperty("texture_asset_id").ValueKind == JsonValueKind.Number)
        {
            _texture2dAssetId = AssetInfo.GuidsById[element.GetProperty("texture_asset_id").GetInt32()];
        }
        else
        {
            _texture2dAssetId = element.GetProperty("texture_asset_id").GetGuid();
        }

        //if (!string.IsNullOrEmpty(AssetInfo.FileName) && File.Exists(AssetInfo.FileName))
        //{
        //    LoadTexture(AssetInfo.FileName);
        //}
    }

#if EDITOR

    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        jObject.Add("texture_asset_id", _texture2dAssetId);

        var newNode = new JObject();
        PreferredSamplerState.Save(newNode);
        jObject.Add("sampler_state", newNode);
    }
#endif
}