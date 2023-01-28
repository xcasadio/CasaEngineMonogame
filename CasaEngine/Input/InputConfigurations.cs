using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Extension;
using Microsoft.Xna.Framework.Input;
using CasaEngine;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Input
{
    /// <summary>
    /// 
    /// </summary>
    public class InputConfigurations
    {

        Dictionary<string, ButtonConfiguration> m_Configs = new Dictionary<string, ButtonConfiguration>();





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



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <returns></returns>
        public ButtonConfiguration GetConfig(string name_)
        {
            return m_Configs[name_];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="config_"></param>
        public void AddConfig(string name_, ButtonConfiguration config_)
        {
            m_Configs.Add(name_, config_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="newConfig_"></param>
        public void ReplaceButton(string name_, ButtonConfiguration newConfig_)
        {
            m_Configs[name_] = newConfig_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        public void DeleteButton(string name_)
        {
            m_Configs.Remove(name_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Save(XmlElement el_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bw_,"></param>
        /// <param name="option_"></param>
        public void Save(BinaryWriter bw_, SaveOption option_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(BinaryReader br_, SaveOption option_)
        {

        }

    }
}
