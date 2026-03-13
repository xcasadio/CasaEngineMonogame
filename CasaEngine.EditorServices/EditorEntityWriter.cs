using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public static class EditorEntityWriter
{
    public static void SaveEntity(Entity entity)
    {
        JObject rootObject = new();
        entity.Save(rootObject);
        EditorAssetWriterService.SaveDocument(entity.FileName, rootObject);
    }
}