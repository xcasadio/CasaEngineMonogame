using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Xml;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;
using Microsoft.Xna.Framework.Input;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen.Gadget
{
    /// <summary>
    /// 
    /// </summary>
    public abstract
#if EDITOR
    partial
#endif
    class ScreenGadget
        : ISaveLoad
    {
        #region Fields

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

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public Rectangle Bounds
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        protected Texture2D WhiteTexture
        {
            get { return m_WhiteTexture; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Renderer2DComponent Renderer2DComponent
        {
            get { return m_Renderer2DComponent; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public bool AutoSize
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public bool CanSelect
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int TabIndex
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Color BackgroundColor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Color FontColor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public SpriteFont Font
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string FontName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string Text
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Vector2 Location
        {
            get { return m_Location; }
            set 
            { 
                m_Location = value;
                UpdateBounds();
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int Width
        {
            get { return m_Width; }
            set
            { 
                m_Width = value;
                UpdateBounds();
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public int Height
        {
            get { return m_Height; }
            set
            {
                m_Height = value;
                UpdateBounds();
            }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public Vector2 Scale
        {
            get;
            set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        protected ScreenGadget(XmlElement el_, SaveOption opt_)
        {
            Load(el_, opt_);
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public void Draw(float elapsedTime_)
        {
            DrawGadget(elapsedTime_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
#if EDITOR
        public // used by ScreenGadgetManipulator
#else
        protected
#endif
        abstract void DrawGadget(float elapsedTime_);

        #region Load

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="br_"></param>
        /// <param name="opt_"></param>
        public virtual void Load(BinaryReader br_, SaveOption opt_)
        {
            throw new NotImplementedException();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        private void UpdateBounds()
        {
            Bounds = new Rectangle((int)Location.X, (int)Location.Y, m_Width, m_Height);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="g_"></param>
        /// <returns></returns>
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

        #endregion

        #region Factory

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        /// <returns></returns>
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

        #endregion
    }
}
