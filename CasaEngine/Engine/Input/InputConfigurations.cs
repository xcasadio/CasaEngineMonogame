using System.Text.Json;
using CasaEngine.Engine.Input.InputSequence;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Input;

public class InputConfigurations
{
    private readonly Dictionary<string, ButtonConfiguration> _configs = new();

    public InputConfigurations()
    {
        /* ButtonConfiguration buttonConfig = new ButtonConfiguration();

         ButtonMapper buttonMapper = new ButtonMapper();
         buttonMapper.Name = "Forward";
         buttonMapper.Buttons = Buttons.DPadRight | Buttons.LeftThumbstickRight;
         buttonMapper.Key = Keys.Right;
         buttonConfig.AddButton((int)FightingGame.Character.CommandButton.Forward, buttonMapper);

         _Configs.Add("default", buttonConfig);*/
    }

    public ButtonConfiguration GetConfig(string name)
    {
        return _configs[name];
    }

    public void AddConfig(string name, ButtonConfiguration config)
    {
        _configs.Add(name, config);
    }

    public void ReplaceButton(string name, ButtonConfiguration newConfig)
    {
        _configs[name] = newConfig;
    }

    public void DeleteButton(string name)
    {
        _configs.Remove(name);
    }

    public void Save(JObject jObject)
    {

    }

    public void Load(JsonElement element)
    {

    }
}