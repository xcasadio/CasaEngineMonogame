using System.Text.Json;
using CasaEngine.Helpers;

namespace CasaEngine.Entities.Components;

public static class ComponentLoader
{
    private static readonly Dictionary<int, Func<BaseObject, JsonElement, Component>> FactoryByIds = new();

    public static void Register<T>(int id, Func<BaseObject, Component> factoryFunc) where T : Component
    {
        if (FactoryByIds.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        FactoryByIds[id] = (owner, element) =>
        {
            var component = factoryFunc.Invoke(owner);
            component.Load(element);
            return component;
        };
    }

    public static void Register(int id, Func<BaseObject, JsonElement, Component> factoryFunc)
    {
        if (FactoryByIds.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        FactoryByIds[id] = factoryFunc;
    }

    public static Component Load(BaseObject owner, JsonElement element)
    {
        var id = element.GetJsonPropertyByName("Type").Value.GetInt32();
        return FactoryByIds[id].Invoke(owner, element);
    }
}