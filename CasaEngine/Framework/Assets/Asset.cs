using System.Text.Json;
using CasaEngine.Core.Design;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public abstract class Asset : Disposable, ISaveLoad
{
    public AssetInfo AssetInfo { get; } = new();

    protected Asset()
    {
        //Do nothing
    }

    internal virtual void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        //override
    }

    public virtual void Load(JsonElement element, SaveOption option)
    {
        AssetInfo.Load(element, option);
    }

#if EDITOR
    public virtual void Save(JObject jObject, SaveOption option)
    {
        AssetInfo.Save(jObject, option);
    }
#endif
}