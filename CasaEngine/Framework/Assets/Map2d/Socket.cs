using System.Text.Json;
using CasaEngine.Core.Helpers;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Map2d;

public class Socket
{
    public string Name;
    public Point Position;

    public void Load(JsonElement jsonElement)
    {
        Name = jsonElement.GetJsonPropertyByName("name").Value.GetString();
        Position = jsonElement.GetJsonPropertyByName("position").Value.GetPoint();
    }

    public void Save(JObject jObject)
    {
        jObject.Add("name", Name);
        var newjObject = new JObject();
        Position.Save(newjObject);
        jObject.Add("position", newjObject);
    }
}