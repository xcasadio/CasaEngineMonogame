using CasaEngine.Framework.Scene.World;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public static class EditorWorldWriter
{
    public static void SaveWorld(World world)
    {
        JObject rootObject = new();
        EditorEntityJsonSerializer.SaveWorld(world, rootObject);
        EditorAssetWriterService.SaveDocument(world.FileName, rootObject);
    }
}