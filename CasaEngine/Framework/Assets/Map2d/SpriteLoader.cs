using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Assets.Map2d;

public class SpriteLoader
{
    public static List<SpriteData> LoadFromFile(string fileName, AssetContentManager assetContentManager)
    {
        List<SpriteData> spriteDatas = new List<SpriteData>();

        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;

        foreach (var jsonElement in rootElement.GetJsonPropertyByName("sprites").Value.EnumerateArray())
        {
            var spriteData = new SpriteData();
            spriteData.Load(jsonElement);
            spriteData.FileName = fileName;
            assetContentManager.AddAsset(spriteData.Name, spriteData);
            spriteDatas.Add(spriteData);
        }

        return spriteDatas;
    }
}