using CasaEngine.Framework.World;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public static class EditorWorldWriter
{
    public static void SaveWorld(World world)
    {
        JObject rootObject = new();
        world.Save(rootObject);
        EditorAssetWriterService.SaveDocument(world.FileName, rootObject);
    }
}