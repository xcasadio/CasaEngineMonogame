using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Engine;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLoader
{
    public static TileMapData LoadMapFromFile(string fileName)
    {
        fileName = Path.Combine(EngineEnvironment.ProjectPath, fileName);
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var tileMapData = new TileMapData();
        tileMapData.Load(jsonDocument.RootElement, SaveOption.Editor);
        return tileMapData;
    }

#if EDITOR
    public static void Save(string file, TileMapData tileMapData)
    {

    }
#endif
}