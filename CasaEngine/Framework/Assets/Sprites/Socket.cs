
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class Socket
{
    public string Name;
    public Point Position;

    public void Load(JObject JObject)
    {
        Name = JObject["name"].GetString();
        Position = JObject["position"].GetPoint();
    }

    public void Save(JObject jObject)
    {
        jObject.Add("name", Name);
        var newjObject = new JObject();
        Position.Save(newjObject);
        jObject.Add("position", newjObject);
    }
}