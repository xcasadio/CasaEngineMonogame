using System.Text.Json;
using CasaEngine.Core.Logs;
using CasaEngine.Core.Serialization;

namespace CasaEngine.Framework.Assets;

public class ElementFactory<T> where T : class
{
    private readonly Dictionary<Guid, Type> _typesById = new();

    public void Register<TU>(Guid id) where TU : class, new()
    {
        if (_typesById.ContainsKey(id))
        {
            throw new ArgumentException("the id already exist");
        }

        _typesById[id] = typeof(TU);
    }

    public object Create(Guid id)
    {
        if (!_typesById.ContainsKey(id))
        {
            LogManager.Instance.WriteError($"The component with the type {id} is not supported. Please Register it before load it.");
        }

        return Activator.CreateInstance(_typesById[id]);
    }

    public TU Load<TU>(JsonElement element) where TU : class, ISerializable
    {
        var id = element.GetProperty("type").GetGuid();
        var component = Create(id) as TU;
        component.Load(element);
        return component;
    }

#if EDITOR

    public KeyValuePair<Guid, Type>[] TypesById => _typesById.ToArray();

    public Type GetTypeFromId(Guid id)
    {
        _typesById.TryGetValue(id, out var type);
        return type;
    }

    public IEnumerable<KeyValuePair<Guid, Type>> GetDerivedTypesFrom<T>() where T : class
    {
        foreach (var pair in _typesById
                     .Where(x => x.Value is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }))
        {
            if (pair.Value.IsSubclassOf(typeof(T)))
            {
                yield return pair;
            }
        }
    }

#endif
}