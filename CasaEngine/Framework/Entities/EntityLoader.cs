using System.Text.Json;

namespace CasaEngine.Framework.Entities;

public static class EntityLoader
{
    public static Entity Load(JsonElement element)
    {
        var baseObject = new Entity();
        baseObject.Load(element);
        return baseObject;
    }

    public static List<Entity> LoadFromArray(JsonElement element)
    {
        var baseObjects = new List<Entity>();

        foreach (var item in element.EnumerateArray())
        {
            baseObjects.Add(Load(item));
        }

        return baseObjects;
    }
}