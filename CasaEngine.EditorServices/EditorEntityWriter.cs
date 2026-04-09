using CasaEngine.Framework.Scene.Entities;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public static class EditorEntityWriter
{
    public static void SaveEntity(Entity entity)
    {
        JObject rootObject = new();
        EditorEntityJsonSerializer.SaveEntity(entity, rootObject);
        EditorAssetWriterService.SaveDocument(entity.FileName, rootObject);
    }
}