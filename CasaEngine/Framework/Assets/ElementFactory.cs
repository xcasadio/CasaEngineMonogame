using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class ElementFactory
{
    private static Dictionary<string, Type> _typeCache;

    static ElementFactory()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => RebuildCaches();
    }

    public static T Create<T>(string typeName) where T : class
    {
        var type = FindTypeByName(typeName);
        return Activator.CreateInstance(type) as T;
    }

    public static T Load<T>(JObject element) where T : class, ISerializable
    {
        var typeName = element["type"].GetString();
        var component = Create<T>(typeName);
        component.Load(element);
        return component;
    }

    private static void RebuildCaches()
    {
        _typeCache = BuildTypeCache();
        _derivedTypesCache = _derivedTypesCache.Keys.ToDictionary(t => t, BuildDerivedTypes);
    }

    private static Dictionary<string, Type> BuildTypeCache() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase);

    private static Type FindTypeByName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        _typeCache ??= BuildTypeCache();
        _typeCache.TryGetValue(typeName, out var type);
        return type;
    }

    private static Dictionary<Type, IEnumerable<Type>> _derivedTypesCache = new();

    private static IEnumerable<Type> BuildDerivedTypes(Type type) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                        && x.IsSubclassOf(type))
            .ToList();

    public static IEnumerable<Type> GetDerivedTypesFrom<T>() where T : class
    {
        var type = typeof(T);
        if (!_derivedTypesCache.TryGetValue(type, out var derived))
        {
            derived = BuildDerivedTypes(type);
            _derivedTypesCache[type] = derived;
        }
        return derived;
    }
}