using CasaEngine.Graphics2D;
using System.Xml;
using CasaEngineCommon.Design;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngine.UserInterface;
using Control = XNAFinalEngine.UserInterface.Control;

namespace CasaEngine.FrontEnd.Screen
{
    public
#if EDITOR
    partial
#endif
    class UIScreen
        : Screen
    {
        readonly List<Control> m_Controls = new List<Control>();



        public Control[] Gagdets => m_Controls.ToArray();


        public UIScreen(string name_)
            : base(name_)
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public UIScreen(XmlElement el_, SaveOption opt_)
            : base(el_, opt_)
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }



        public override void LoadContent(Renderer2DComponent r_)
        {
            base.LoadContent(r_);

            /*foreach (Control g in m_Controls)
            {
                g.Initialize(r_.Game);
            }*/
        }

        public override void Draw(float elapsedTime_)
        {
            /*foreach (Control g in m_Controls)
            {
                g.Draw(elapsedTime_);
            }*/
        }

        public override void HandleInput(InputState input)
        {

        }

        public override void Update(float elapsedTime_, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(elapsedTime_, otherScreenHasFocus, coveredByOtherScreen);

            foreach (Control g in m_Controls)
            {
                g.Update(elapsedTime_);
            }
        }


        public Control GetGadget(string name_)
        {
            foreach (Control g in m_Controls)
            {
                if (g.Name.Equals(name_) == true)
                {
                    return g;
                }
            }

            return null;
        }

        public override void CopyFrom(Screen screen_)
        {
            base.CopyFrom(screen_);

            if (screen_ is UIScreen)
            {
                UIScreen e = screen_ as UIScreen;
                m_Controls.Clear();
                m_Controls.AddRange(e.m_Controls);
            }
            else
            {
                throw new InvalidCastException("EditorScreen.CopyFrom() : Screen is not a EditorScreen");
            }
        }

#if EDITOR

        public override bool CompareTo(BaseObject other_)
        {
            if (base.CompareTo(other_) == false)
            {
                return false;
            }

            bool res = true;

            if (other_ is UIScreen)
            {
                UIScreen e = other_ as UIScreen;

                if (m_Controls.Count != e.m_Controls.Count)
                {
                    res = false;
                }
                else
                {
                    for (int i = 0; i < m_Controls.Count; i++)
                    {
                        //res &= e.m_Controls[i].Compare(m_Controls[i]);
                    }
                }
            }
            else
            {
                throw new InvalidCastException("EditorScreen.Compare() : Screen is not a EditorScreen");
            }

            return res;
        }

#endif

        public override void Load(XmlElement el_, SaveOption opt_)
        {
            base.Load(el_, opt_);

            XmlNode nodeList = el_.SelectSingleNode("GadgetList");

            foreach (XmlNode node in nodeList.ChildNodes)
            {
                m_Controls.Add(FactoryUIControl.LoadControl((XmlElement)node, opt_));
            }
        }
    }
}
