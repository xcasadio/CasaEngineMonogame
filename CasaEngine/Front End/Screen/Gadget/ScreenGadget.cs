using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Xml;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;
using Microsoft.Xna.Framework.Input;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    public abstract
#if EDITOR
    partial
#endif
    class ScreenGadget
        : ISaveLoad
    {

        private Texture2D m_WhiteTexture;

        public event EventHandler Click;
        public event EventHandler MouseEnter;
        public event EventHandler MouseMove;
        public event EventHandler MouseLeave;
        public event EventHandler SelectedChanged;

        private Renderer2DComponent m_Renderer2DComponent;
        private int m_Width;
        private int m_Height;
        private Vector2 m_Location;

        private bool m_MouseLeftPressed = false;
        private bool m_MouseOver = false;



        public Rectangle Bounds
        {
            get;
            protected set;
        }

        protected Texture2D WhiteTexture => m_WhiteTexture;

        public Renderer2DComponent Renderer2DComponent => m_Renderer2DComponent;

        public bool AutoSize
        {
            get;
            set;
        }

        public bool CanSelect
        {
            get;
            set;
        }

        public int TabIndex
        {
            get;
            set;
        }

        public Color BackgroundColor
        {
            get;
            set;
        }

        public Color FontColor
        {
            get;
            set;
        }

        public SpriteFont Font
        {
            get;
            set;
        }

        public string FontName
        {
            get;
            set;
        }

        public string Text
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public Vector2 Location
        {
            get => m_Location;
            set
            {
                m_Location = value;
                UpdateBounds();
            }
        }

        public int Width
        {
            get => m_Width;
            set
            {
                m_Width = value;
                UpdateBounds();
            }
        }

        public int Height
        {
            get => m_Height;
            set
            {
                m_Height = value;
                UpdateBounds();
            }
        }

        public Vector2 Scale
        {
            get;
            set;
        }



        protected ScreenGadget(XmlElement el_, SaveOption opt_)
        {
            Load(el_, opt_);
        }



        public virtual void Initialize(Microsoft.Xna.Framework.Game game_)
        {
            m_Renderer2DComponent = GameHelper.GetDrawableGameComponent<Renderer2DComponent>(game_);

            //TODO : faire autrement
            m_WhiteTexture = new Texture2D(game_.GraphicsDevice, 1, 1);
            Color[] whitePixels = new Color[] { Color.White };
            m_WhiteTexture.SetData<Color>(whitePixels);

            Font = Engine.Instance.DefaultSpriteFont;
            //Font = game_.Content.Load<SpriteFont>(FontName);
        }

        public virtual void Update(float elapsedTime_)
        {
            int mouseX = Mouse.GetState().X, mouseY = Mouse.GetState().Y;
            bool mouseOver = Bounds.Contains(mouseX, mouseY);

            if (Mouse.GetState().LeftButton == ButtonState.Pressed)
            {
                if (m_MouseLeftPressed == false
                    && mouseOver == true)
                {
                    if (Click != null)
                    {
                        Click.Invoke(this, EventArgs.Empty);
                    }
                }

                m_MouseLeftPressed = true;
            }
            else
            {
                m_MouseLeftPressed = false;
            }

            if (mouseOver == true
                && m_MouseOver == false)
            {
                if (MouseEnter != null)
                {
                    MouseEnter.Invoke(this, EventArgs.Empty);
                }
            }
            else if (mouseOver == false
                && m_MouseOver == true)
            {
                if (MouseLeave != null)
                {
                    MouseLeave.Invoke(this, EventArgs.Empty);
                }
            }
            else if (mouseOver == true)
            {
                mouseOver = true;

                if (MouseMove != null)
                {
                    MouseMove.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void Draw(float elapsedTime_)
        {
            DrawGadget(elapsedTime_);
        }

#if EDITOR
        public // used by ScreenGadgetManipulator
#else
        protected
#endif
        abstract void DrawGadget(float elapsedTime_);


        public virtual void Load(XmlElement el_, SaveOption opt_)
        {
            Color c = Color.White;
            Vector2 v = Vector2.Zero;

            int version = int.Parse(el_.Attributes["version"].Value);

            AutoSize = bool.Parse(el_.SelectSingleNode("AutoSize").InnerText);
            ((XmlElement)el_.SelectSingleNode("BackgroundColor")).Read(ref c);
            BackgroundColor = c;
            FontName = el_.SelectSingleNode("FontName").InnerText;
            ((XmlElement)el_.SelectSingleNode("FontColor")).Read(ref c);
            FontColor = c;
            Width = int.Parse(el_.SelectSingleNode("Width").InnerText);
            Height = int.Parse(el_.SelectSingleNode("Height").InnerText);
            TabIndex = int.Parse(el_.SelectSingleNode("TabIndex").InnerText);
            ((XmlElement)el_.SelectSingleNode("Location")).Read(ref v);
            Location = v;
            ((XmlElement)el_.SelectSingleNode("Scale")).Read(ref v);
            Scale = v;
            Name = el_.SelectSingleNode("Name").InnerText;
            Text = el_.SelectSingleNode("Text").InnerText;
        }

        public virtual void Load(BinaryReader br_, SaveOption opt_)
        {
            throw new NotImplementedException();
        }


        private void UpdateBounds()
        {
            Bounds = new Rectangle((int)Location.X, (int)Location.Y, m_Width, m_Height);
        }

        public bool Compare(ScreenGadget g_)
        {
            return m_Width == g_.m_Width
                && m_Height == g_.m_Height
                && m_Location == g_.m_Location
                && AutoSize == g_.AutoSize
                && CanSelect == g_.CanSelect
                && TabIndex == g_.TabIndex
                && BackgroundColor == g_.BackgroundColor
                && FontColor == g_.FontColor
                && Font == g_.Font
                && Text == g_.Text
                && Name == g_.Name
                && Scale == g_.Scale;
        }



        static public ScreenGadget LoadScreenGadget(XmlElement el_, SaveOption opt_)
        {
            string typeName = el_.Attributes["typeName"].Value;

            switch (typeName)
            {
                case "Label":
                    return new ScreenGadgetLabel(el_, opt_);

                case "Button":
                    return new ScreenGadgetButton(el_, opt_);

                default:
                    throw new InvalidOperationException("ScreenGadget.LoadScreen() : the screen type " + typeName + " is not supported.");
            }
        }

    }
}
