using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets.Animations;

public class Animation2dLoader
{
    private const string Animation2dDatasNodeName = "animations";

    public static List<Animation2dData> LoadFromFile(string fileName, AssetContentManager assetContentManager)
    {
        List<Animation2dData> animations = new();
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var rootElement = jsonDocument.RootElement;

        foreach (var jsonElement in rootElement.GetJsonPropertyByName(Animation2dDatasNodeName).Value.EnumerateArray())
        {
            var animation2dData = new Animation2dData();
            animation2dData.Load(jsonElement, SaveOption.Editor);
            animation2dData.FileName = fileName;
            assetContentManager.AddAsset(animation2dData.Name, animation2dData);
            animations.Add(animation2dData);
        }

        return animations;
    }

#if EDITOR
    public static void SaveToFile(string fileName, IEnumerable<Animation2dData> animation2dDatas)
    {
        JObject rootJObject = new();
        var spriteJArray = new JArray();

        foreach (var data in animation2dDatas)
        {
            JObject entityObject = new();
            data.Save(entityObject, SaveOption.Editor);
            spriteJArray.Add(entityObject);
        }

        rootJObject.Add(Animation2dDatasNodeName, spriteJArray);

        var fullFileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName);
        using StreamWriter file = File.CreateText(fullFileName);
        using JsonTextWriter writer = new JsonTextWriter(file) { Formatting = Formatting.Indented };
        rootJObject.WriteTo(writer);
    }

#endif
}