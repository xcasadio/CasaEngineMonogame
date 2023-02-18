using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;

namespace Editor.Game
{
    /// <summary>
    /// 
    /// </summary>
    class MyOwnEditorGame
        : Microsoft.Xna.Framework.Game
    {

        public Form AttachedForm;
        private Microsoft.Xna.Framework.GraphicsDeviceManager m_GraphicsDeviceManager;
        private Form GameForm;



        /// <summary>
        /// Gets
        /// </summary>
        public Microsoft.Xna.Framework.GraphicsDeviceManager GraphicsDeviceManager
        {
            get { return m_GraphicsDeviceManager; }
        }



        /// <summary>
        /// Create components
        /// Initialize GraphicsDeviceManager
        /// Read Arguments
        /// </summary>
        public MyOwnEditorGame()
            : base()
        {
            Window.Title = "Editor";
            Window.AllowUserResizing = true;
            IsFixedTimeStep = true;
            IsMouseVisible = true;

            Content.RootDirectory = /*Environment.CurrentDirectory + */"Content";

            m_GraphicsDeviceManager = new Microsoft.Xna.Framework.GraphicsDeviceManager(this);
            m_GraphicsDeviceManager.PreparingDeviceSettings += new EventHandler<PreparingDeviceSettingsEventArgs>(graphics_PreparingDeviceSettings);

            //Grid2DComponent g = new Grid2DComponent(this);

            Exiting += new EventHandler<EventArgs>(EditorGame_Exiting);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            for (int i = 0; i < GraphicsAdapter.Adapters.Count; i++)
            {
                if (GraphicsAdapter.Adapters[i].IsProfileSupported(GraphicsProfile.HiDef))
                {
                    e.GraphicsDeviceInformation.Adapter = GraphicsAdapter.Adapters[i];
                    e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
                    break;
                }
            }

            e.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = AttachedForm.Height;
            e.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = AttachedForm.Width;
        }

        /// <summary>
        /// Occurs when the original gamewindows' visibility changes and makes sure it stays invisible
        /// </summary>
        ///
        private void xnaScreen_VisibleChanged(object sender, EventArgs e)
        {
            if (AttachedForm.Visible == true)
            {
                AttachedForm.Visible = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void xnaScreen_Disposed(object sender, EventArgs e)
        {
        }

        delegate void SetDisposeCallback();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditorGame_Exiting(object sender, EventArgs e)
        {
            if (AttachedForm.Disposing == false)
            {
                if (AttachedForm.InvokeRequired)
                {
                    SetDisposeCallback c = new SetDisposeCallback(DisposeCallback);
                    AttachedForm.Invoke(c);
                }
                else
                {
                    DisposeCallback();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DisposeCallback()
        {
            if (AttachedForm != null
                && AttachedForm.IsDisposed == false)
            {
                AttachedForm.Dispose();
            }
        }



        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            if (AttachedForm == null)
            {
                throw new InvalidOperationException("Set AttachedForm before call Run().");
            }

            base.Initialize();

            GameForm = (Form)System.Windows.Forms.Control.FromHandle(Window.Handle);
            GameForm.FormBorderStyle = FormBorderStyle.SizableToolWindow;

            AttachedForm.VisibleChanged += new EventHandler(xnaScreen_VisibleChanged);
            AttachedForm.Disposed += new EventHandler(xnaScreen_Disposed);
            AttachedForm.LocationChanged += new EventHandler(AttachedForm_LocationChanged);
            AttachedForm.Resize += new EventHandler(AttachedForm_LocationChanged);
            GameForm.LocationChanged += new EventHandler(AttachedForm_LocationChanged);
            AttachedForm_LocationChanged(null, EventArgs.Empty);
        }

        delegate void SetLocationCallback();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AttachedForm_LocationChanged(object sender, EventArgs e)
        {
            if (GameForm.InvokeRequired)
            {
                SetLocationCallback c = new SetLocationCallback(LocationCallback);
                GameForm.Invoke(c);
            }
            else
            {
                LocationCallback();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void LocationCallback()
        {
            GameForm.Location = new System.Drawing.Point(AttachedForm.Width + AttachedForm.Location.X, AttachedForm.Location.Y);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            Renderer2DComponent r = this.GetGameComponent<Renderer2DComponent>();
            r.SpriteBatch = new SpriteBatch(GraphicsDevice);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            base.Draw(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="width_"></param>
        /// <param name="height_"></param>
        /*public void Resize(int width_, int height_)
        {
            m_GraphicsDeviceManager.PreferredBackBufferWidth = width_;
            m_GraphicsDeviceManager.PreferredBackBufferHeight = height_;
            m_GraphicsDeviceManager.ApplyChanges();
        }*/

        delegate void SetFormEnabledCallback();

        /// <summary>
        /// 
        /// </summary>
        protected override void BeginRun()
        {
            if (AttachedForm != null)
            {
                SetFormEnabledCallback c = new SetFormEnabledCallback(FormEnabledCallback);
                AttachedForm.Invoke(c);
            }

            base.BeginRun();
        }

        /// <summary>
        /// 
        /// </summary>
        private void FormEnabledCallback()
        {
            AttachedForm.Enabled = true;
        }

    }
}
