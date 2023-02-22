using CasaEngine.Core.Helper;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Debugger;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game
{
    public class CasaEngineGame : Microsoft.Xna.Framework.Game
    {
#if !FINAL
        protected string ContentPath = string.Empty;
#endif
        private readonly GraphicsDeviceManager _graphicsDeviceManager;
        private readonly Renderer2DComponent _renderer2DComponent;
        private ScreenManagerComponent _screenManagerComponent;
        private InputComponent _inputComponent;
        private ShapeRendererComponent _shapeRendererComponent;
        private MeshRendererComponent _meshRendererComponent;

        private string ProjectFile { get; } = string.Empty;

        public CasaEngineGame()
        {
            Engine.Instance.Game = this;

            _graphicsDeviceManager = new GraphicsDeviceManager(this);
            _graphicsDeviceManager.PreparingDeviceSettings += PreparingDeviceSettings;

            Engine.Instance.AssetContentManager = new AssetContentManager();
            Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Texture2D), new Texture2DLoader());
            Engine.Instance.AssetContentManager.RegisterAssetLoader(typeof(Cursor), new CursorLoader());

            DebugSystem.Initialize(this);

            _renderer2DComponent = new Renderer2DComponent(this);
            _inputComponent = new InputComponent(this);
            _screenManagerComponent = new ScreenManagerComponent(this);
            _shapeRendererComponent = new ShapeRendererComponent(this);
            _meshRendererComponent = new MeshRendererComponent(this);
            var gizmoComponent = new GizmoComponent(this);


#if EDITOR
            new GridComponent(this);
#endif

#if !FINAL
            var args = Environment.CommandLine.Split(' ');

            if (args.Length > 1)
            {
                ProjectFile = args[1];
            }

            ContentPath = args.Length > 2 ? args[2] : Path.Combine(Directory.GetCurrentDirectory(), "Content");
#else
            ContentPath = "Content";
#endif
        }

        private void PreparingDeviceSettings(object? sender, PreparingDeviceSettingsEventArgs e)
        {
            e.GraphicsDeviceInformation.GraphicsProfile =
                GraphicsAdapter.Adapters.Any(x => x.IsProfileSupported(GraphicsProfile.HiDef)) ? GraphicsProfile.HiDef : GraphicsProfile.Reach;
        }

        protected override void Initialize()
        {
            Content.RootDirectory = ContentPath;
            //CasaEngine.Game.Engine.Instance.ProjectManager.Load(ProjectFile);
            //TODO : create hierarchy of the project
            if (!string.IsNullOrWhiteSpace(ProjectFile))
            {
                Engine.Instance.ProjectSettings.Load(ProjectFile);
            }

            Window.Title = Engine.Instance.ProjectSettings.WindowTitle;
            Window.AllowUserResizing = Engine.Instance.ProjectSettings.AllowUserResizing;
            IsFixedTimeStep = Engine.Instance.ProjectSettings.IsFixedTimeStep;
            IsMouseVisible = Engine.Instance.ProjectSettings.IsMouseVisible;

#if !FINAL
            _graphicsDeviceManager.PreferredBackBufferWidth = Engine.Instance.ProjectSettings.DebugWidth;
            _graphicsDeviceManager.PreferredBackBufferHeight = Engine.Instance.ProjectSettings.DebugHeight;
#else
            //recuperer la resolution des options
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
#endif

            _graphicsDeviceManager.ApplyChanges();

            if (!string.IsNullOrWhiteSpace(Engine.Instance.ProjectSettings.GameplayDllName))
            {
                Engine.Instance.PluginManager.Load(Engine.Instance.ProjectSettings.GameplayDllName);
            }

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Engine.Instance.AssetContentManager.Initialize(GraphicsDevice);
            Engine.Instance.AssetContentManager.RootDirectory = ContentPath;

            //CasaEngine.Game.Engine.Instance.UiManager.Initialize(GraphicsDevice, Window.Handle, Window.ClientBounds);

            Engine.Instance.SpriteBatch = new SpriteBatch(GraphicsDevice);
            //TODO : defaultSpriteFont
            //GameInfo.Instance.DefaultSpriteFont = Content.Load<SpriteFont>("Content/defaultSpriteFont");

            _renderer2DComponent.SpriteBatch = Engine.Instance.SpriteBatch;

            base.LoadContent();

            if (GameInfo.Instance.CurrentWorld == null)
            {
                if (string.IsNullOrWhiteSpace(Engine.Instance.ProjectSettings.FirstWorldLoaded))
                {
                    throw new InvalidOperationException("FirstWorldLoaded is undefined");
                }

                GameInfo.Instance.CurrentWorld = new World.World();
                GameInfo.Instance.CurrentWorld.Load(Engine.Instance.ProjectSettings.FirstWorldLoaded);
            }

            GameInfo.Instance.CurrentWorld.Initialize();

            _shapeRendererComponent.SetCurrentPhysicsWorld(GameInfo.Instance.CurrentWorld.PhysicWorld);
        }

        protected override void Update(GameTime gameTime)
        {
#if !FINAL
            DebugSystem.Instance.TimeRuler.StartFrame();
            DebugSystem.Instance.TimeRuler.BeginMark("Update", Color.Blue);
#endif

            //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
            //    DebugSystem.Instance.DebugCommandUI.Show(); 

            var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
            GameInfo.Instance.CurrentWorld?.Update(elapsedTime);
            //CasaEngine.Game.Engine.Instance.UiManager.Update(time);
            base.Update(gameTime);

#if !FINAL
            DebugSystem.Instance.TimeRuler.EndMark("Update");
#endif
        }

        protected override void Draw(GameTime gameTime)
        {
#if !FINAL
            DebugSystem.Instance.TimeRuler.StartFrame();
            DebugSystem.Instance.TimeRuler.BeginMark("Draw", Color.Blue);
#endif

            //GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.Clear(Color.Black);

            var elapsedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
            GameInfo.Instance.CurrentWorld.Draw(elapsedTime);
            //CasaEngine.Game.Engine.Instance.UiManager.PreRenderControls();

            base.Draw(gameTime);

            //CasaEngine.Game.Engine.Instance.UiManager.RenderUserInterfaceToScreen();
#if !FINAL
            DebugSystem.Instance.TimeRuler.EndMark("Draw");
#endif
        }
    }
}
