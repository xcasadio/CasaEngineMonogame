using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Serialization;

namespace CasaEngine.Engine.Input;

public class ButtonsMapping : ObjectBase
{
    public List<Button> Buttons { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);

        var buttons = element.GetElements("buttons", t =>
        {
            var button = new Button();
            button.Load((JObject)t);
            return button;
        });

        Buttons.AddRange(buttons);
    }

#if EDITOR

    public override void Save(JObject node)
    {
        base.Save(node);

        var buttonArrayNode = new JArray();

        foreach (var button in Buttons)
        {
            var buttonNode = new JObject();
            button.Save(buttonNode);
            buttonArrayNode.Add(buttonNode);

        }

        node.Add("buttons", buttonArrayNode);
    }

#endif
}