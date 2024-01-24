using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace CasaEngine.EditorUI.Plugins.Tools;

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
            .Where(x => !x.IsAbstract && !x.IsInterface && componentType.IsAssignableFrom(x) && CustomAttributeExtensions.GetCustomAttribute<DisplayNameAttribute>((MemberInfo)x) != null)
            .ToDictionary(x => x.GetCustomAttribute<DisplayNameAttribute>().DisplayName);
    }
}