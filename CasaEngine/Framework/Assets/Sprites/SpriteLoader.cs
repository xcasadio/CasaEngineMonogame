using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteLoader
{
    private const string SpriteDatasNodeName = "sprites";

    public static List<SpriteData> LoadFromFile(string fileName, AssetContentManager assetContentManager)
    {
        List<SpriteData> spriteDatas = new List<SpriteData>();

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;

        foreach (var jsonElement in rootElement.GetJsonPropertyByName(SpriteDatasNodeName).Value.EnumerateArray())
        {
            var spriteData = new SpriteData();
            spriteData.Load(jsonElement);
            //var rect = spriteData.PositionInTexture;
            //rect.X -= 1;
            //rect.Width += 2;
            //rect.Y -= 1;
            //rect.Height += 2;
            //spriteData.PositionInTexture = rect;
            spriteData.FileName = fileName;
            assetContentManager.AddAsset(spriteData.Name, spriteData);
            spriteDatas.Add(spriteData);
        }

        //SaveToFile(fileName, spriteDatas);

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

        var fullFileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName);
        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        rootJObject.WriteTo(writer);
    }

#endif
}