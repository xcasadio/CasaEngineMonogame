using System.Text.Json;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteLoader
{
    private const string SpriteDatasNodeName = "sprites";

    public static List<SpriteData> LoadFromFile(string fileName, AssetContentManager assetContentManager, SaveOption option)
    {
        List<SpriteData> spriteDatas = new List<SpriteData>();

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;

        foreach (var jsonElement in rootElement.GetJsonPropertyByName(SpriteDatasNodeName).Value.EnumerateArray())
        {
            var spriteData = new SpriteData();
            spriteData.Load(jsonElement);
            assetContentManager.AddAsset(spriteData.Id, spriteData.Name, spriteData);
            spriteDatas.Add(spriteData);
        }

        return spriteDatas;
    }

#if EDITOR
    public static void SaveToFile(string fileName, IEnumerable<SpriteData> spriteDatas)
    {
        JObject rootJObject = new();
        var spriteJArray = new JArray();

        foreach (var spriteData in spriteDatas)
        {
            JObject entityObject = new();
            spriteData.Save(entityObject);
            spriteJArray.Add(entityObject);
        }

        rootJObject.Add(SpriteDatasNodeName, spriteJArray);

        var fullFileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        rootJObject.WriteTo(writer);
    }

#endif
}