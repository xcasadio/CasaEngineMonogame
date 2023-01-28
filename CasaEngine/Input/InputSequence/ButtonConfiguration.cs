using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Input;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngine;
using System.Collections;
using Microsoft.Xna.Framework;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.Input
{
    /// <summary>
    /// 
    /// </summary>
    public class ButtonConfiguration
    {

        Dictionary<int, ButtonMapper> m_ButtonsConfig = new Dictionary<int, ButtonMapper>();



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public PlayerIndex PlayerIndex
        {
            get;
            set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public int ButtonCount
        {
            get { return m_ButtonsConfig.Count; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Dictionary<int, ButtonMapper>.Enumerator Buttons
        {
            get { return m_ButtonsConfig.GetEnumerator(); }
        }





        /// <summary>
        /// 
        /// </summary>
        /// <param name="code_"></param>
        /// <returns></returns>
        public ButtonMapper GetButton(int code_)
        {
            return m_ButtonsConfig[code_];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code_"></param>
        /// <param name="but_"></param>
        public void AddButton(int code_, ButtonMapper but_)
        {
            m_ButtonsConfig.Add(code_, but_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code_"></param>
        /// <param name="newBut_"></param>
        public void ReplaceButton(int code_, ButtonMapper newBut_)
        {
            m_ButtonsConfig[code_] = newBut_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code_"></param>
        public void DeleteButton(int code_)
        {
            m_ButtonsConfig.Remove(code_);
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
        /// <param name="el_"></param>
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
