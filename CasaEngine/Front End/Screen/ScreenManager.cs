using System.Xml;
using CasaEngineCommon.Design;

namespace CasaEngine.Front_End.Screen
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
            var version = int.Parse(el.Attributes["version"].Value);

            var nodeList = el.SelectSingleNode("ScreenList");

            _screens.Clear();

            foreach (XmlNode node in nodeList)
            {
                _screens.Add(new UiScreen((XmlElement)node, opt));
            }
        }

    }
}
