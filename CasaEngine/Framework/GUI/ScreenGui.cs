
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;
using Control = TomShane.Neoforce.Controls.Control;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.Framework.GUI;

public class ScreenGui : Entity
{
    private readonly List<Control> _controls = new();

    public IEnumerable<Control> Controls => _controls;
    private ScreenWidgetComponent ScreenWidgetComponent { get; }

    public ScreenGui()
    {
        ScreenWidgetComponent = new ScreenWidgetComponent();
        RootComponent = ScreenWidgetComponent;
    }

    public void Add(Control control)
    {
        _controls.Add(control);

#if EDITOR
        ScreenWidgetComponent.Owner.World.Game.UiManager.Add(control);
#endif
    }

    public void Remove(Control control)
    {
        _controls.Remove(control);

#if EDITOR
        ScreenWidgetComponent.Owner.World.Game.UiManager.Remove(control);
#endif
    }

    public Control? GetControlByName(string name)
    {
        foreach (var control in _controls)
        {
            if (control.Name == name)
            {
                return control;
            }
        }

        return null;
    }

    public void Draw()
    {
        //do nothing
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var externalComponentElement = element["external_component"];
        if (externalComponentElement.GetString() != "null")
        {
            //GameplayProxy = ;
        }

        foreach (var controlElement in element["controls"])
        {
            var typeName = controlElement["type"].GetString();

            var control = ControlHelper.Load(typeName, (JObject)controlElement);
            _controls.Add(control);
        }
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);

        if (GameplayProxy != null)
        {
            var externalComponentNode = new JObject();
            GameplayProxy.Save(externalComponentNode);
            jObject.Add("external_component", externalComponentNode);
        }
        else
        {
            jObject.Add("external_component", "null");
        }

        var controlsNode = new JArray();

        foreach (var control in _controls)
        {
            var controlNode = new JObject();
            control.Save(controlNode);
            controlsNode.Add(controlNode);
        }

        jObject.Add("controls", controlsNode);
    }
#endif
}