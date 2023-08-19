using CasaEngine.Core.Design;
using System.Text.Json;

namespace CasaEngine.Framework.Entities;

public static class EntityLoader
{
    public static Entity Load(JsonElement element, SaveOption option)
    {
        var baseObject = new Entity();
        baseObject.Load(element, option);
        return baseObject;
    }

    public static List<Entity> LoadFromArray(JsonElement element, SaveOption option)
    {
        var baseObjects = new List<Entity>();

        foreach (var item in element.EnumerateArray())
        {
            baseObjects.Add(Load(item, option));
        }

        return baseObjects;
    }
}