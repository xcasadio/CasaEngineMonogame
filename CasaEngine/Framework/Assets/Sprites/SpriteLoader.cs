using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Sprites;

public class SpriteLoader
{
    private const string SpriteDatasNodeName = "sprites";

    public static List<SpriteData> LoadFromFile(string fileName, AssetContentManager assetContentManager)
    {
        List<SpriteData> spriteDatas = new List<SpriteData>();

        JObject rootElement = JObject.Parse(File.ReadAllText(fileName));

        foreach (var spriteNode in rootElement["sprites"])
        {
            var spriteData = new SpriteData();
            spriteData.Load((JObject)spriteNode);
            assetContentManager.AddAsset(spriteData.Id, spriteData.Name, spriteData);
            spriteDatas.Add(spriteData);
        }

        return spriteDatas;
    }
}