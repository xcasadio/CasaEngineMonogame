using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.SceneManagement.Components;

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
        var componentType = typeof(EntityComponent);

        EntityComponentNames = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(t => t is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                        && t.IsSubclassOf(componentType) && t.GetCustomAttribute<DisplayNameAttribute>() != null)
            .ToDictionary(x => x.GetCustomAttribute<DisplayNameAttribute>().DisplayName);
    }
}