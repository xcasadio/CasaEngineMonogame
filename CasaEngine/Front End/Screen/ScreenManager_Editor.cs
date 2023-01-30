using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    public partial class ScreenManager
    {

        static private readonly int m_Version = 1;







        public bool IsValidName(string name_)
        {
            foreach (UIScreen screen in m_Screens)
            {
                if (screen.Name.Equals(name_) == true)
                {
                    return false;
                }
            }

            return true;
        }

        public UIScreen GetScreen(string name_)
        {
            foreach (UIScreen screen in m_Screens)
            {
                if (screen.Name.Equals(name_) == true)
                {
                    return screen;
                }
            }

            throw new InvalidOperationException("Screenmanager.GetScreen() : can't find the screen " + name_);
        }

        public void AddScreen(UIScreen screen_)
        {
            m_Screens.Add(screen_);
        }

        public void RemoveScreen(UIScreen screen_)
        {
            m_Screens.Remove(screen_);
        }

        public void RemoveScreen(string name_)
        {
            UIScreen s = null;

            foreach (UIScreen screen in m_Screens)
            {
                if (screen.Name.Equals(name_) == true)
                {
                    RemoveScreen(s);
                    return;
                }
            }

            throw new InvalidOperationException("Screenmanager.RemoveScreen() : can't find the screen " + name_);
        }

        public void Save(XmlElement el_, SaveOption opt_)
        {
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());

            XmlElement nodeList = el_.OwnerDocument.CreateElement("ScreenList");
            el_.AppendChild(nodeList);

            foreach (UIScreen screen in m_Screens)
            {
                XmlElement node = el_.OwnerDocument.CreateElement("Screen");
                nodeList.AppendChild(node);

                screen.Save(node, opt_);
            }
        }

        public void Save(BinaryWriter bw_, SaveOption opt_)
        {
            bw_.Write(m_Version);
            bw_.Write(m_Screens.Count);

            foreach (UIScreen screen in m_Screens)
            {
                screen.Save(bw_, opt_);
            }
        }

    }
}
