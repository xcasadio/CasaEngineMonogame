using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public enum SaveOption
{
    Editor,
    Game
}

public interface ISaveLoad
{
    void Load(JsonElement element, SaveOption option);

#if EDITOR
    void Save(JObject jObject, SaveOption option);
#endif
}


public interface ISerializable
{
    void Load(JsonElement element);

#if EDITOR
    void Save(JObject node);
#endif
}