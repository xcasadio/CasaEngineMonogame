using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Input.InputSequence;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input;

public class InputConfigurations : ISerializable
{
    private readonly Dictionary<string, ButtonConfiguration> _configurations = new();

    public InputConfigurations()
    {
        ButtonConfiguration buttonConfig = new ButtonConfiguration();
        _configurations.Add("default", buttonConfig);

        buttonConfig.AddButton((int)CommandButton.Forward, new ButtonMapper
        {
            Name = nameof(CommandButton.Forward),
            Buttons = Buttons.DPadRight | Buttons.LeftThumbstickRight,
            Key = Keys.Right
        });

        buttonConfig.AddButton((int)CommandButton.Backward, new ButtonMapper
        {
            Name = nameof(CommandButton.Backward),
            Buttons = Buttons.DPadLeft | Buttons.LeftThumbstickLeft,
            Key = Keys.Left
        });


        buttonConfig.AddButton((int)CommandButton.Right, new ButtonMapper
        {
            Name = nameof(CommandButton.Right),
            Buttons = Buttons.DPadRight | Buttons.LeftThumbstickRight,
            Key = Keys.Right
        });

        buttonConfig.AddButton((int)CommandButton.Left, new ButtonMapper
        {
            Name = nameof(CommandButton.Left),
            Buttons = Buttons.DPadLeft | Buttons.LeftThumbstickLeft,
            Key = Keys.Left
        });

        buttonConfig.AddButton((int)CommandButton.Up, new ButtonMapper
        {
            Name = nameof(CommandButton.Up),
            Buttons = Buttons.DPadUp | Buttons.LeftThumbstickUp,
            Key = Keys.Up
        });

        buttonConfig.AddButton((int)CommandButton.Down, new ButtonMapper
        {
            Name = nameof(CommandButton.Down),
            Buttons = Buttons.DPadDown | Buttons.LeftThumbstickDown,
            Key = Keys.Down
        });


        buttonConfig.AddButton((int)CommandButton.Pause, new ButtonMapper
        {
            Name = nameof(CommandButton.Pause),
            Buttons = Buttons.Start,
            Key = Keys.Escape
        });


        buttonConfig.AddButton((int)CommandButton.Attack, new ButtonMapper
        {
            Name = nameof(CommandButton.Attack),
            Buttons = Buttons.A,
            Key = Keys.Space
        });
    }

    public ButtonConfiguration GetConfig(string name)
    {
        return _configurations[name];
    }

    public void AddConfig(string name, ButtonConfiguration config)
    {
        _configurations.Add(name, config);
    }

    public void ReplaceButton(string name, ButtonConfiguration newConfig)
    {
        _configurations[name] = newConfig;
    }

    public void DeleteButton(string name)
    {
        _configurations.Remove(name);
    }

    public void Save(JObject jObject)
    {
        var configurationNodes = new JArray();

        foreach (var configuration in _configurations)
        {
            var configNode = new JObject();
            configurationNodes.Add(configNode);
            configNode["key"] = configuration.Key;

            var configurationNode = new JArray();
            configNode.Add("buttons_configuration", configurationNode);

            var buttonEnumerator = configuration.Value.Buttons;
            while (buttonEnumerator.MoveNext())
            {
                var buttonPairNode = new JObject();
                buttonPairNode["key"] = buttonEnumerator.Current.Key;
                var buttonMapperNode = new JObject();
                buttonPairNode["value"] = buttonMapperNode;
                buttonMapperNode["name"] = buttonEnumerator.Current.Value.Name;
                buttonMapperNode["key"] = (int)buttonEnumerator.Current.Value.Key;
                buttonMapperNode["alt_key"] = (int)buttonEnumerator.Current.Value.AltKey;
                buttonMapperNode["buttons"] = (int)buttonEnumerator.Current.Value.Buttons;
            }
        }

        jObject.Add("configurations", configurationNodes);
    }

    public void Load(JObject element)
    {

    }
}