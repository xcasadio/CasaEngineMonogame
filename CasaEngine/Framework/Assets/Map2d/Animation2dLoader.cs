using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Assets.Animations;

namespace CasaEngine.Framework.Assets.Map2d;

public class Animation2dLoader
{
    public static List<Animation2dData> LoadFromFile(string fileName, AssetContentManager assetContentManager)
    {
        List<Animation2dData> animations = new();
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;

        foreach (var jsonElement in rootElement.GetJsonPropertyByName("animations").Value.EnumerateArray())
        {
            var animation2dData = new Animation2dData();
            animation2dData.Load(jsonElement);
            animation2dData.FileName = fileName;
            assetContentManager.AddAsset(animation2dData.Name, animation2dData);
            animations.Add(animation2dData);
        }

        return animations;
    }
}