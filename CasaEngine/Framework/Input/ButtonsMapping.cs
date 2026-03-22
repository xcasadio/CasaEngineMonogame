using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Input;

public class ButtonsMapping : ObjectBase
{
    public List<InputMapping> Buttons { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        var buttons = element.GetElements("buttons", t =>
        {
            var button = new InputMapping();
            button.Load((JObject)t);
            return button;
        });

        Buttons.AddRange(buttons);
    }

}