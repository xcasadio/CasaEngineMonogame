using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Core.Design;

public class ElementLoader<T> where T : ISaveLoad
{
    private readonly Dictionary<int, Func<Entity, JsonElement, T>> FactoryByIds = new();

    public void Register(int id, Func<Entity, T> factoryFunc)
    {
        if (FactoryByIds.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        FactoryByIds[id] = (owner, element) =>
        {
            var component = factoryFunc.Invoke(owner);
            component.Load(element, SaveOption.Editor);
            return component;
        };
    }

    public void Register(int id, Func<Entity, JsonElement, T> factoryFunc)
    {
        if (FactoryByIds.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        FactoryByIds[id] = factoryFunc;
    }

    public T Load(Entity owner, JsonElement element)
    {
        var id = element.GetJsonPropertyByName("type").Value.GetInt32();

        if (!FactoryByIds.ContainsKey(id))
        {
            LogManager.Instance.WriteLineError($"The component with the type {id} is not supported. Please Register it before load it.");
        }

        return FactoryByIds[id].Invoke(owner, element);
    }
}