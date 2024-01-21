using System.ComponentModel;
using System.Reflection;

namespace CasaEngine.Editor.Tools;

public static class ElementRegister
{
    public static IReadOnlyDictionary<string, Type> EntityComponentNames { get; private set; }

    static ElementRegister()
    {
        SetEntityComponentNames();
    }

    private static void SetEntityComponentNames()
    {
        var componentType = typeof(Component);
        var executingAssembly = Assembly.GetExecutingAssembly();

        EntityComponentNames = executingAssembly
            .GetTypes()
            .Where(x => !x.IsAbstract && !x.IsInterface && componentType.IsAssignableFrom(x) && x.GetCustomAttribute<DisplayNameAttribute>() != null)
            .ToDictionary(x => x.GetCustomAttribute<DisplayNameAttribute>().DisplayName);
    }
}