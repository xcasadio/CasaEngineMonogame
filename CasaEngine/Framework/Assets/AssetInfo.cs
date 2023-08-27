using System.Text.Json;
using CasaEngine.Core.Design;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public class AssetInfo : ISaveLoad
{
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

    public AssetInfo(bool initialize = true)
    {
        if (initialize)
        {
            Id = IdManager.GetId();
            _name = "Asset_" + Id;
        }
    }

    public virtual void Load(JsonElement element, SaveOption option)
    {
        Id = element.GetProperty("id").GetInt64();
        IdManager.SetMax(Id);
        Name = element.GetProperty("name").GetString();

        if (element.TryGetProperty("file_name", out var node))
        {
            FileName = element.GetProperty("file_name").GetString();
        }
    }

#if EDITOR
    public virtual void Save(JObject jObject, SaveOption option)
    {
        var assetObject = new JObject(
            new JProperty("id", Id),
            new JProperty("name", Name),
            new JProperty("file_name", FileName));

        jObject.Add("asset", assetObject);
    }
#endif
}