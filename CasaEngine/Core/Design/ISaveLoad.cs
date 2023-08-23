using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Design;

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