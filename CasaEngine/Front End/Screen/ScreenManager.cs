using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    public
#if EDITOR
    partial
#endif
    class ScreenManager
    {
        readonly List<UIScreen> m_Screens = new List<UIScreen>();








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
