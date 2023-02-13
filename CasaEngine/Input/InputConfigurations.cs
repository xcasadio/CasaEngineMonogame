using System.Xml;
using CasaEngine.Input.InputSequence;
using CasaEngineCommon.Design;

namespace CasaEngine.Input
{
    public class InputConfigurations
    {
        readonly Dictionary<string, ButtonConfiguration> _configs = new();





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

        public void Save(XmlElement el, SaveOption option)
        {

        }

        public void Load(XmlElement el, SaveOption option)
        {

        }

        public void Save(BinaryWriter bw, SaveOption option)
        {

        }

        public void Load(BinaryReader br, SaveOption option)
        {

        }

    }
}
