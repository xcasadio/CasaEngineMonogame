using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Debugger;
using CasaEngine.Graphics2D;
using CasaEngine.FrontEnd.Screen;
using CasaEngine.Input;
using CasaEngine.Helper;
using CasaEngineCommon.Helper;
using CasaEngine.Asset;

namespace CasaEngine.Game
{
    public abstract class CasaEngineGame
        : Microsoft.Xna.Framework.Game
    {
        private readonly Microsoft.Xna.Framework.GraphicsDeviceManager graphics;
        private readonly Renderer2DComponent m_Renderer2DComponent;
        private ScreenManagerComponent m_ScreenManagerComponent;
        private InputComponent m_InputComponent;
        private ShapeRendererComponent m_ShapeRendererComponent;

        protected string m_ProjectFile = string.Empty;
#if !FINAL
        protected string m_ContentPath = string.Empty;
#endif


        public Microsoft.Xna.Framework.GraphicsDeviceManager GraphicsDeviceManager => graphics;

        public string ProjectFile => m_ProjectFile;


        public CasaEngineGame()
        {
            Engine.Instance.Game = this;

            graphics = new Microsoft.Xna.Framework.GraphicsDeviceManager(this);
            graphics.PreparingDeviceSettings += new EventHandler<PreparingDeviceSettingsEventArgs>(graphics_PreparingDeviceSettings);

            Engine.Instance.AssetContentManager = new AssetContentManager();
            Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
            Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(System.Windows.Forms.Cursor), new CursorLoader());

            DebugSystem.Initialize(this);

            m_Renderer2DComponent = new Renderer2DComponent(this);
            m_InputComponent = new InputComponent(this);
            m_ScreenManagerComponent = new ScreenManagerComponent(this);
            m_ShapeRendererComponent = new ShapeRendererComponent(this);

#if !FINAL
            string[] args = Environment.CommandLine.Split(' ');

            if (args.Length > 1)
            {
                m_ProjectFile = args[1];
            }

            m_ContentPath = Directory.GetCurrentDirectory();

            if (args.Length > 2)
            {
                m_ContentPath = args[2];
            }
#endif
        }


        void graphics_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            for (int i = 0; i < GraphicsAdapter.Adapters.Count; i++)
            {
                if (GraphicsAdapter.Adapters[i].IsProfileSupported(GraphicsProfile.HiDef))
                {
                    //e.GraphicsDeviceInformation.Adapter = GraphicsAdapter.Adapters[i];
                    e.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
                    break;
                }
            }
        }

        protected override void Initialize()
        {
#if FINAL
            Content.RootDirectory = "Content";
#else
            Content.RootDirectory = m_ContentPath;
#endif

            Engine.Instance.ProjectManager.Load(m_ProjectFile);

#if !FINAL
            graphics.PreferredBackBufferWidth = Engine.Instance.ProjectConfig.DebugWidth;
            graphics.PreferredBackBufferHeight = Engine.Instance.ProjectConfig.DebugHeight;
#else
            //recuperer la resolution des optionsS
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
#endif

            this.Window.Title = Engine.Instance.ProjectConfig.WindowTitle;
            this.Window.AllowUserResizing = Engine.Instance.ProjectConfig.AllowUserResizing;
            this.IsFixedTimeStep = Engine.Instance.ProjectConfig.IsFixedTimeStep;
            this.IsMouseVisible = Engine.Instance.ProjectConfig.IsMouseVisible;

            graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Engine.Instance.AssetContentManager.Initialize(GraphicsDevice);
            Engine.Instance.AssetContentManager.RootDirectory = m_ContentPath;

            Engine.Instance.UIManager.Initialize(GraphicsDevice, Window.Handle, Window.ClientBounds);

            Engine.Instance.SpriteBatch = new SpriteBatch(GraphicsDevice);
            //TODO : defaultSpriteFont
            //GameInfo.Instance.DefaultSpriteFont = Content.Load<SpriteFont>("Content/defaultSpriteFont");

            m_Renderer2DComponent.SpriteBatch = Engine.Instance.SpriteBatch;
        }

        protected override void BeginRun()
        {
            //test
            //GameInfo.Instance.WorldInfo.World = new World();
            //m_ScreenManagerComponent.AddScreen(new WorldScreen(GameInfo.Instance.WorldInfo.World, "world test"), PlayerIndex.One);
            base.BeginRun();
        }

        protected abstract void Update(float elpasedTime_);

        protected override void Update(GameTime gameTime)
        {
            if (Engine.Instance.ResetDevice == true)
            {
                GraphicsDeviceManager.ApplyChanges();
                Engine.Instance.ResetDevice = false;
            }

#if !FINAL
            DebugSystem.Instance.TimeRuler.StartFrame();
            DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif // !FINAL

            //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
            //    DebugSystem.Instance.DebugCommandUI.Show(); 

            float time = GameTimeHelper.GameTimeToMilliseconds(gameTime);
            Engine.Instance.UIManager.Update(time);
            base.Update(gameTime);
            Update(time);

#if !FINAL
            DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif // !FINAL
        }

        protected abstract void Draw(float elpasedTime_);

        protected override void Draw(GameTime gameTime)
        {
#if !FINAL
            DebugSystem.Instance.TimeRuler.StartFrame();
            DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif // !FINAL

            float time = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            Engine.Instance.UIManager.PreRenderControls();

            Draw(time);
            base.Draw(gameTime);

            Engine.Instance.UIManager.RenderUserInterfaceToScreen();
#if !FINAL
            DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif // !FINAL
        }
    }
}
