using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    /// <summary>
    /// 
    /// </summary>
    public
#if EDITOR
    partial
#endif
    class ScreenManager
    {
        List<UIScreen> m_Screens = new List<UIScreen>();








        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public void Load(XmlElement el_, SaveOption opt_)
        {
            int version = int.Parse(el_.Attributes["version"].Value);

            XmlNode nodeList = el_.SelectSingleNode("ScreenList");

            m_Screens.Clear();

            foreach (XmlNode node in nodeList)
            {
                m_Screens.Add(new UIScreen((XmlElement)node, opt_));
            }
        }

    }
}
