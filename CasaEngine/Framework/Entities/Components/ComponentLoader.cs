using System.Text.Json;
using CasaEngine.Core.Helpers;

namespace CasaEngine.Framework.Entities.Components;

public static class ComponentLoader
{
    private static readonly Dictionary<int, Func<Entity, JsonElement, Component>> FactoryByIds = new();

    public static void Register<T>(int id, Func<Entity, Component> factoryFunc) where T : Component
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

    public static void Register(int id, Func<Entity, JsonElement, Component> factoryFunc)
    {
        if (FactoryByIds.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        FactoryByIds[id] = factoryFunc;
    }

    public static Component Load(Entity owner, JsonElement element)
    {
        var id = element.GetJsonPropertyByName("type").Value.GetInt32();
        return FactoryByIds[id].Invoke(owner, element);
    }
}