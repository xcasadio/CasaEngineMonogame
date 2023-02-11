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
        readonly List<UiScreen> _screens = new();








        public void Load(XmlElement el, SaveOption opt)
        {
            int version = int.Parse(el.Attributes["version"].Value);

            XmlNode nodeList = el.SelectSingleNode("ScreenList");

            _screens.Clear();

            foreach (XmlNode node in nodeList)
            {
                _screens.Add(new UiScreen((XmlElement)node, opt));
            }
        }

    }
}
