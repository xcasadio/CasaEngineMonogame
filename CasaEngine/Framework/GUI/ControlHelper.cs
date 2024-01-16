using System.Reflection;
using System.Text.Json;
using TomShane.Neoforce.Controls;
using Button = TomShane.Neoforce.Controls.Button;
using Control = TomShane.Neoforce.Controls.Control;

namespace CasaEngine.Framework.GUI;

public static class ControlHelper
{
    static public IDictionary<string, Type> TypesByName { get; }

    static ControlHelper()
    {
        TypesByName = AppDomain.CurrentDomain.GetAssemblies().
            SelectMany(x => x.GetTypes())
            .Where(t => t is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                    && t.IsSubclassOf(typeof(Control)))
            .ToDictionary(x => x.Name, x => x);
    }

    public static Control Load(string typeName, JsonElement element)
    {
        var control = (Control)Activator.CreateInstance(TypesByName[typeName]);
        control.Load(element);
        return control;
    }
}