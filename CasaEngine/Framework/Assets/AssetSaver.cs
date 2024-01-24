using CasaEngine.Core.Serialization;
using CasaEngine.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class AssetSaver
{
#if EDITOR

    public static void SaveAsset(string fileName, ISerializable asset)
    {
        JObject rootObject = new();
        asset.Save(rootObject);

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        rootObject.WriteTo(writer);
    }

#endif
}