using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.FrontEnd.Screen.Gadget
{
    public abstract
#if EDITOR
    partial
#endif
    class ScreenGadget
        : ISaveLoad
    {

        private Texture2D _whiteTexture;

        public event EventHandler Click;
        public event EventHandler MouseEnter;
        public event EventHandler MouseMove;
        public event EventHandler MouseLeave;
        public event EventHandler SelectedChanged;

        private Renderer2DComponent _renderer2DComponent;
        private int _width;
        private int _height;
        private Vector2 _location;

        private bool _mouseLeftPressed;
        private bool _mouseOver = false;



        public Rectangle Bounds
        {
            get;
            protected set;
        }

        protected Texture2D WhiteTexture => _whiteTexture;

        public Renderer2DComponent Renderer2DComponent => _renderer2DComponent;

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
            get => _location;
            set
            {
                _location = value;
                UpdateBounds();
            }
        }

        public int Width
        {
            get => _width;
            set
            {
                _width = value;
                UpdateBounds();
            }
        }

        public int Height
        {
            get => _height;
            set
            {
                _height = value;
                UpdateBounds();
            }
        }

        public Vector2 Scale
        {
            get;
            set;
        }



        protected ScreenGadget(XmlElement el, SaveOption opt)
        {
            Load(el, opt);
        }



        public virtual void Initialize(Microsoft.Xna.Framework.Game game)
        {
            _renderer2DComponent = game.GetDrawableGameComponent<Renderer2DComponent>();

            //TODO : faire autrement
            _whiteTexture = new Texture2D(game.GraphicsDevice, 1, 1);
            var whitePixels = new Color[] { Color.White };
            _whiteTexture.SetData<Color>(whitePixels);

            Font = Framework.Game.Engine.Instance.DefaultSpriteFont;
            //Font = game_.Content.Load<SpriteFont>(FontName);
        }

        public virtual void Update(float elapsedTime)
        {
            int mouseX = Mouse.GetState().X, mouseY = Mouse.GetState().Y;
            var mouseOver = Bounds.Contains(mouseX, mouseY);

            if (Mouse.GetState().LeftButton == ButtonState.Pressed)
            {
                if (_mouseLeftPressed == false
                    && mouseOver)
                {
                    if (Click != null)
                    {
                        Click.Invoke(this, EventArgs.Empty);
                    }
                }

                _mouseLeftPressed = true;
            }
            else
            {
                _mouseLeftPressed = false;
            }

            if (mouseOver
                && _mouseOver == false)
            {
                if (MouseEnter != null)
                {
                    MouseEnter.Invoke(this, EventArgs.Empty);
                }
            }
            else if (mouseOver == false
                && _mouseOver)
            {
                if (MouseLeave != null)
                {
                    MouseLeave.Invoke(this, EventArgs.Empty);
                }
            }
            else if (mouseOver)
            {
                mouseOver = true;

                if (MouseMove != null)
                {
                    MouseMove.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void Draw(float elapsedTime)
        {
            DrawGadget(elapsedTime);
        }

#if EDITOR
        public // used by ScreenGadgetManipulator
#else
        protected
#endif
        abstract void DrawGadget(float elapsedTime);


        public virtual void Load(XmlElement el, SaveOption opt)
        {
            var c = Color.White;
            var v = Vector2.Zero;

            var version = int.Parse(el.Attributes["version"].Value);

            AutoSize = bool.Parse(el.SelectSingleNode("AutoSize").InnerText);
            ((XmlElement)el.SelectSingleNode("BackgroundColor")).Read(ref c);
            BackgroundColor = c;
            FontName = el.SelectSingleNode("FontName").InnerText;
            ((XmlElement)el.SelectSingleNode("FontColor")).Read(ref c);
            FontColor = c;
            Width = int.Parse(el.SelectSingleNode("Width").InnerText);
            Height = int.Parse(el.SelectSingleNode("Height").InnerText);
            TabIndex = int.Parse(el.SelectSingleNode("TabIndex").InnerText);
            ((XmlElement)el.SelectSingleNode("Location")).Read(ref v);
            Location = v;
            ((XmlElement)el.SelectSingleNode("Scale")).Read(ref v);
            Scale = v;
            Name = el.SelectSingleNode("Name").InnerText;
            Text = el.SelectSingleNode("Text").InnerText;
        }

        public virtual void Load(BinaryReader br, SaveOption opt)
        {
            throw new NotImplementedException();
        }


        private void UpdateBounds()
        {
            Bounds = new Rectangle((int)Location.X, (int)Location.Y, _width, _height);
        }

        public bool Compare(ScreenGadget g)
        {
            return _width == g._width
                && _height == g._height
                && _location == g._location
                && AutoSize == g.AutoSize
                && CanSelect == g.CanSelect
                && TabIndex == g.TabIndex
                && BackgroundColor == g.BackgroundColor
                && FontColor == g.FontColor
                && Font == g.Font
                && Text == g.Text
                && Name == g.Name
                && Scale == g.Scale;
        }



        public static ScreenGadget LoadScreenGadget(XmlElement el, SaveOption opt)
        {
            var typeName = el.Attributes["typeName"].Value;

            switch (typeName)
            {
                case "Label":
                    return new ScreenGadgetLabel(el, opt);

                case "Button":
                    return new ScreenGadgetButton(el, opt);

                default:
                    throw new InvalidOperationException("ScreenGadget.LoadScreen() : the screen type " + typeName + " is not supported.");
            }
        }

    }
}
