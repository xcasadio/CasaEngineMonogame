using CasaEngine.Framework.Entities;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Objects;

namespace CasaEngine.Engine.Input;

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