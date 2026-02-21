
using Newtonsoft.Json.Linq;

namespace CasaEngine.Core.Serialization;

public interface ISerializable
{
    public void Load(JObject element);

#if EDITOR
    void Save(JObject node);
#endif
}