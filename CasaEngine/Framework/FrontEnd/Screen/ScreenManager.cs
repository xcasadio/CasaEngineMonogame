using System.Xml;
using CasaEngine.Core.Design;

namespace CasaEngine.Framework.FrontEnd.Screen
{
    public
#if EDITOR
    partial
#endif
    class ScreenManager
    {
        private readonly List<UiScreen> _screens = new();








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
