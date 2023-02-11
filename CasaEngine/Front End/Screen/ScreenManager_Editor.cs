using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    public partial class ScreenManager
    {

        static private readonly int Version = 1;







        public bool IsValidName(string name)
        {
            foreach (UiScreen screen in _screens)
            {
                if (screen.Name.Equals(name) == true)
                {
                    return false;
                }
            }

            return true;
        }

        public UiScreen GetScreen(string name)
        {
            foreach (UiScreen screen in _screens)
            {
                if (screen.Name.Equals(name) == true)
                {
                    return screen;
                }
            }

            throw new InvalidOperationException("Screenmanager.GetScreen() : can't find the screen " + name);
        }

        public void AddScreen(UiScreen screen)
        {
            _screens.Add(screen);
        }

        public void RemoveScreen(UiScreen screen)
        {
            _screens.Remove(screen);
        }

        public void RemoveScreen(string name)
        {
            UiScreen s = null;

            foreach (UiScreen screen in _screens)
            {
                if (screen.Name.Equals(name) == true)
                {
                    RemoveScreen(s);
                    return;
                }
            }

            throw new InvalidOperationException("Screenmanager.RemoveScreen() : can't find the screen " + name);
        }

        public void Save(XmlElement el, SaveOption opt)
        {
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            XmlElement nodeList = el.OwnerDocument.CreateElement("ScreenList");
            el.AppendChild(nodeList);

            foreach (UiScreen screen in _screens)
            {
                XmlElement node = el.OwnerDocument.CreateElement("Screen");
                nodeList.AppendChild(node);

                screen.Save(node, opt);
            }
        }

        public void Save(BinaryWriter bw, SaveOption opt)
        {
            bw.Write(Version);
            bw.Write(_screens.Count);

            foreach (UiScreen screen in _screens)
            {
                screen.Save(bw, opt);
            }
        }

    }
}
