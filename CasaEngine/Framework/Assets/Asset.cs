using System.Text.Json;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public abstract class Asset : Disposable
{
    //public event EventHandler? Disposed;

    private string _name;

    public long Id { get; private set; }

    public string FileName { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (!string.IsNullOrEmpty(value) && _name != value)
            {
                _name = value;
            }
        }
    }

    protected Asset()
    {
        Id = IdManager.GetId();
    }

    internal virtual void OnDeviceReset(GraphicsDevice device, AssetContentManager assetContentManager)
    {
        //override
    }

    public virtual void Load(BinaryReader br, SaveOption option)
    {

    }

    public virtual void Load(XmlElement el, SaveOption option)
    {

    }

    public virtual void Load(JsonElement element)
    {
        var version = element.GetJsonPropertyByName("version").Value.GetInt32();
        Name = element.GetJsonPropertyByName("name").Value.GetString();
        FileName = element.GetJsonPropertyByName("file_name").Value.GetString();
        Id = element.GetJsonPropertyByName("id").Value.GetInt32();
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        var assetObject = new JObject(
            new JProperty("version", 1),
            new JProperty("id", Id),
            new JProperty("name", Name),
            new JProperty("file_name", FileName));

        jObject.Add("asset", assetObject);
    }
#endif
}