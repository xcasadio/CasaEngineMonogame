using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Serialization;

public interface ISerializable
{
    void Load(JsonElement element);

#if EDITOR
    void Save(JObject node);
#endif
}