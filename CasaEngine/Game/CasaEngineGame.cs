using CasaEngine.Assets;
using CasaEngine.Assets.Loaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Debugger;
using CasaEngine.Front_End.Screen;
using CasaEngine.Graphics2D;
using CasaEngine.Helpers;
using CasaEngine.Input;
using CasaEngineCommon.Helper;

namespace CasaEngine.Game
{
    public abstract class CasaEngineGame : Microsoft.Xna.Framework.Game
    {
        private readonly Microsoft.Xna.Framework.GraphicsDeviceManager _graphics;
        private readonly Renderer2DComponent _renderer2DComponent;
        private ScreenManagerComponent _screenManagerComponent;
        private InputComponent _inputComponent;
        private ShapeRendererComponent _shapeRendererComponent;

#if !FINAL
        protected string ContentPath = string.Empty;
#endif

        public Microsoft.Xna.Framework.GraphicsDeviceManager GraphicsDeviceManager => _graphics;

        public string ProjectFile { get; } = string.Empty;

        public CasaEngineGame()
        {
            Engine.Instance.Game = this;

            _graphics = new Microsoft.Xna.Framework.GraphicsDeviceManager(this);
            _graphics.PreparingDeviceSettings += PreparingDeviceSettings;

            Engine.Instance.AssetContentManager = new AssetContentManager();
            Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
            Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());

            DebugSystem.Initialize(this);

            _renderer2DComponent = new Renderer2DComponent(this);
            _inputComponent = new InputComponent(this);
            _screenManagerComponent = new ScreenManagerComponent(this);
            _shapeRendererComponent = new ShapeRendererComponent(this);

#if !FINAL
            var args = Environment.CommandLine.Split(' ');

            if (args.Length > 1)
            {
                ProjectFile = args[1];
            }

            ContentPath = Directory.GetCurrentDirectory();

            if (args.Length > 2)
            {
                ContentPath = args[2];
            }
#endif
        }

        private void PreparingDeviceSettings(object? sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.GraphicsProfile =
                GraphicsAdapter.Adapters.Any(x => x.IsProfileSupported(GraphicsProfile.HiDef)) ? GraphicsProfile.HiDef : GraphicsProfile.Reach;
        }

        protected override void Initialize()
        {
#if FINAL
            Content.RootDirectory = "Content";
#else
            Content.RootDirectory = ContentPath;
#endif

            Engine.Instance.ProjectManager.Load(ProjectFile);

#if !FINAL
            _graphics.PreferredBackBufferWidth = Engine.Instance.ProjectConfig.DebugWidth;
            _graphics.PreferredBackBufferHeight = Engine.Instance.ProjectConfig.DebugHeight;
#else
            //recuperer la resolution des options
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
#endif

            Window.Title = Engine.Instance.ProjectConfig.WindowTitle;
            Window.AllowUserResizing = Engine.Instance.ProjectConfig.AllowUserResizing;
            IsFixedTimeStep = Engine.Instance.ProjectConfig.IsFixedTimeStep;
            IsMouseVisible = Engine.Instance.ProjectConfig.IsMouseVisible;

            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Engine.Instance.AssetContentManager.Initialize(GraphicsDevice);
            Engine.Instance.AssetContentManager.RootDirectory = ContentPath;

            //Engine.Instance.UiManager.Initialize(GraphicsDevice, Window.Handle, Window.ClientBounds);

            Engine.Instance.SpriteBatch = new SpriteBatch(GraphicsDevice);
            //TODO : defaultSpriteFont
            //GameInfo.Instance.DefaultSpriteFont = Content.Load<SpriteFont>("Content/defaultSpriteFont");

            _renderer2DComponent.SpriteBatch = Engine.Instance.SpriteBatch;
        }

        protected override void BeginRun()
        {
            //test
            //GameInfo.Instance.WorldInfo.World = new World();
            //_ScreenManagerComponent.AddScreen(new WorldScreen(GameInfo.Instance.WorldInfo.World, "world test"), PlayerIndex.One);
            base.BeginRun();
        }

        protected abstract void Update(float elapsedTime);

        protected override void Update(GameTime gameTime)
        {
            if (Engine.Instance.ResetDevice)
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

            var time = GameTimeHelper.GameTimeToMilliseconds(gameTime);
            //Engine.Instance.UiManager.Update(time);
            base.Update(gameTime);
            Update(time);

#if !FINAL
            DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif // !FINAL
        }

        protected abstract void Draw(float elapsedTime);

        protected override void Draw(GameTime gameTime)
        {
#if !FINAL
            DebugSystem.Instance.TimeRuler.StartFrame();
            DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif // !FINAL

            var time = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            //Engine.Instance.UiManager.PreRenderControls();

            Draw(time);
            base.Draw(gameTime);

            //Engine.Instance.UiManager.RenderUserInterfaceToScreen();
#if !FINAL
            DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif // !FINAL
        }
    }
}
