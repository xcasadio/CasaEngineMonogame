using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework.Graphics;
using Size = CasaEngine.Core.Maths.Size;

namespace CasaEngine.Framework.Assets.TileMap;

public class TileMapLoader
{
    public static TileMapData LoadMapFromFile(string fileName)
    {
        fileName = Path.Combine(GameSettings.ProjectSettings.ProjectPath, fileName);
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