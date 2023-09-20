using System.Text.Json;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Scripting;

namespace CasaEngine.Core.Design;

public class ElementLoader<T> where T : ISaveLoad
{
    private readonly Dictionary<int, Type> _TypesById = new();

    public void Register<TU>(int id) where TU : new()
    {
        if (_TypesById.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        _TypesById[id] = typeof(TU);
    }

    public T Load(Entity owner, JsonElement element)
    {
        var id = element.GetJsonPropertyByName("type").Value.GetInt32();

        if (!_TypesById.ContainsKey(id))
        {
            LogManager.Instance.WriteLineError($"The component with the type {id} is not supported. Please Register it before load it.");
        }

        var component = (T)Activator.CreateInstance(_TypesById[id]);
        component.Load(element, SaveOption.Editor);
        return component;
    }

#if EDITOR

    public KeyValuePair<int, Type>[] TypesById => _TypesById.ToArray();

    public Type GetTypeFromId(int id)
    {
        _TypesById.TryGetValue(id, out var type);
        return type;
    }

    public ExternalComponent Create(int id)
    {
        if (!_TypesById.ContainsKey(id))
        {
            LogManager.Instance.WriteLineError($"The component with the type {id} is not supported. Please Register it before load it.");
        }

        var component = (ExternalComponent)Activator.CreateInstance(_TypesById[id]);
        return component;
    }

#endif
}