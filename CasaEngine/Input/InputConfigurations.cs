using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Input
{
    public class InputConfigurations
    {
        readonly Dictionary<string, ButtonConfiguration> m_Configs = new Dictionary<string, ButtonConfiguration>();





        public InputConfigurations()
        {
            /* ButtonConfiguration buttonConfig = new ButtonConfiguration();

             ButtonMapper buttonMapper = new ButtonMapper();
             buttonMapper.Name = "Forward";
             buttonMapper.Buttons = Buttons.DPadRight | Buttons.LeftThumbstickRight;
             buttonMapper.Key = Keys.Right;
             buttonConfig.AddButton((int)FightingGame.Character.CommandButton.Forward, buttonMapper);

             m_Configs.Add("default", buttonConfig);*/
        }



        public ButtonConfiguration GetConfig(string name_)
        {
            return m_Configs[name_];
        }

        public void AddConfig(string name_, ButtonConfiguration config_)
        {
            m_Configs.Add(name_, config_);
        }

        public void ReplaceButton(string name_, ButtonConfiguration newConfig_)
        {
            m_Configs[name_] = newConfig_;
        }

        public void DeleteButton(string name_)
        {
            m_Configs.Remove(name_);
        }

        public void Save(XmlElement el_, SaveOption option_)
        {

        }

        public void Load(XmlElement el_, SaveOption option_)
        {

        }

        public void Save(BinaryWriter bw_, SaveOption option_)
        {

        }

        public void Load(BinaryReader br_, SaveOption option_)
        {

        }

    }
}
