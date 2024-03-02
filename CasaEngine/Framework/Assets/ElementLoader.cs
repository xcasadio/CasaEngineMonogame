
using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Assets;

public static class ElementFactory
{
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

    private static Type? FindTypeByName(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        //TODO : cache types
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .FirstOrDefault(x => string.Equals(x.Name, typeName, StringComparison.InvariantCultureIgnoreCase));
    }

#if EDITOR

    public static IEnumerable<Type> GetDerivedTypesFrom<T>() where T : class
    {
        var type = typeof(T);
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => x is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                        && x.IsSubclassOf(type));
    }

#endif
}