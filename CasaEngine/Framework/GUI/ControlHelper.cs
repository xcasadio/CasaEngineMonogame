
using Newtonsoft.Json.Linq;
using Control = CasaEngine.Framework.GUI.Neoforce.Control;

namespace CasaEngine.Framework.GUI;

public static class ControlHelper
{
    public static IDictionary<string, Type> TypesByName { get; }

    static ControlHelper()
    {
        TypesByName = AppDomain.CurrentDomain.GetAssemblies().
            SelectMany(x => x.GetTypes())
            .Where(t => t is { IsClass: true, IsGenericType: false, IsInterface: false, IsAbstract: false }
                    && t.IsSubclassOf(typeof(Control)))
            .ToDictionary(x => x.Name, x => x);
    }

    public static Control Load(string typeName, JObject element)
    {
        var control = (Control)Activator.CreateInstance(TypesByName[typeName]);
        control.Load(element);
        return control;
    }
}