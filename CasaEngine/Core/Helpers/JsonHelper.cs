using System.Text.Json;

namespace CasaEngine.Core.Helpers;

public static class JsonHelper
{
    public static JsonProperty GetJsonPropertyByName(this JsonElement jsonElement, string key)
    {
        return jsonElement.EnumerateObject().First(x => x.Name == key);
    }
}