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
    class UiScreen
        : Screen
    {
        readonly List<Control> _controls = new();

        public Control[] Gagdets => _controls.ToArray();

        public UiScreen(string name)
            : base(name)
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public UiScreen(XmlElement el, SaveOption opt)
            : base(el, opt)
        {
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        public override void LoadContent(Renderer2DComponent r)
        {
            base.LoadContent(r);

            /*foreach (Control g in _Controls)
            {
                g.Initialize(r_.Game);
            }*/
        }

        public override void Draw(float elapsedTime)
        {
            /*foreach (Control g in _Controls)
            {
                g.Draw(elapsedTime_);
            }*/
        }

        public override void HandleInput(InputState input)
        {

        }

        public override void Update(float elapsedTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

            foreach (Control g in _controls)
            {
                g.Update(elapsedTime);
            }
        }

        public Control GetGadget(string name)
        {
            foreach (Control g in _controls)
            {
                if (g.Name.Equals(name) == true)
                {
                    return g;
                }
            }

            return null;
        }

        public override void CopyFrom(Screen screen)
        {
            base.CopyFrom(screen);

            if (screen is UiScreen)
            {
                UiScreen e = screen as UiScreen;
                _controls.Clear();
                _controls.AddRange(e._controls);
            }
            else
            {
                throw new InvalidCastException("EditorScreen.CopyFrom() : Screen is not a EditorScreen");
            }
        }

#if EDITOR

        public override bool CompareTo(BaseObject other)
        {
            if (base.CompareTo(other) == false)
            {
                return false;
            }

            bool res = true;

            if (other is UiScreen)
            {
                UiScreen e = other as UiScreen;

                if (_controls.Count != e._controls.Count)
                {
                    res = false;
                }
                else
                {
                    for (int i = 0; i < _controls.Count; i++)
                    {
                        //res &= e._Controls[i].Compare(_Controls[i]);
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

        public override void Load(XmlElement el, SaveOption opt)
        {
            base.Load(el, opt);

            XmlNode nodeList = el.SelectSingleNode("GadgetList");

            foreach (XmlNode node in nodeList.ChildNodes)
            {
                _controls.Add(FactoryUiControl.LoadControl((XmlElement)node, opt));
            }
        }
    }
}
