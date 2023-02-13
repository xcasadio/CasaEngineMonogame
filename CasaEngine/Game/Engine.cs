using CasaEngine.Assets;
using CasaEngine.Core_Systems.Game;
using CasaEngine.Front_End.Screen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Graphics2D;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Gameplay;
using CasaEngine.Project;
using CasaEngine.UserInterface;

#if EDITOR
using CasaEngine.Editor.Tools;
using CasaEngine.Editor.Assets;
#endif

namespace CasaEngine.Game
{
    public class Engine
    {
        private static readonly Engine MsEngine = new();

        private Microsoft.Xna.Framework.Game _game;
        private readonly Asset2DManager _asset2DManager;
        private SpriteFont _defaultSpriteFont;
        private SpriteBatch _spriteBatch;
        private readonly ProjectConfig _projectConfig;
        private readonly ObjectRegistry _objectRegistry;
        private readonly ProjectManager _projectManager;
        //private PackageManager _PackageManager;
        private readonly ObjectManager _objectManager;
        private readonly ScreenManager _screenManager;
        private readonly UserInterfaceManager _uiManager;

#if EDITOR
        private readonly ExternalToolManager _externalToolManager;
        private readonly AssetManager _assetManager;
        private BasicEffect _effect;
#endif
        /*
        private List<string> _Errors = new List<string>();
		private List<string> _Warnings = new List<string>();
		private string[] _Arguments = null;*/

        public static Engine Instance => MsEngine;

        internal bool ResetDevice
        {
            get;
            set;
        }

        public GraphicsDeviceManager GraphicsDeviceManager => (GraphicsDeviceManager)GameHelper.GetService<IGraphicsDeviceManager>(_game);

        public GraphicsProfile GraphicsProfile => _game.GraphicsDevice.GraphicsProfile;

        public AssetContentManager AssetContentManager
        {
            get;
            internal set;
        }

        public Asset2DManager Asset2DManager => _asset2DManager;

        public ProjectConfig ProjectConfig => _projectConfig;

        public ProjectManager ProjectManager => _projectManager;

        /*public PackageManager PackageManager
        {
            get { return _PackageManager; }
        }*/
        public ObjectManager ObjectManager => _objectManager;

        public ScreenManager ScreenManager => _screenManager;

        public UserInterfaceManager UiManager => _uiManager;

        public int MultiSampleQuality { get; set; }

        public Microsoft.Xna.Framework.Game Game
        {
            get => _game;
            set
            {
                if (_game != null)
                {
                    throw new InvalidOperationException("GameInfo.Instance.Game : Game is already set!");
                }

                _game = value;
            }
        }

#if !FINAL

        public SpriteFont DefaultSpriteFont
        {
            get => _defaultSpriteFont;
            set => _defaultSpriteFont = value;
        }

#endif

#if EDITOR

        public BasicEffect BasicEffect
        {
            get => _effect;
            set => _effect = value;
        }

        public AssetManager AssetManager => _assetManager;

        public ExternalToolManager ExternalToolManager => _externalToolManager;

#endif

        public SpriteBatch SpriteBatch
        {
            get => _spriteBatch;
            set => _spriteBatch = value;
        }

        public ObjectRegistry ObjectRegistry => _objectRegistry;

        /*public List<string> Errors
        {
            get { return _Errors; }
        }

        public List<string> Warnings
        {
            get { return _Warnings; }
        }*/

        public string[] Arguments
        {
            get;
            set;
        }

        private Engine()
        {
            _game = null;
            _defaultSpriteFont = null;
            _spriteBatch = null;
            _asset2DManager = new Asset2DManager();
            _projectConfig = new ProjectConfig();
            _objectRegistry = new ObjectRegistry();
            _projectManager = new ProjectManager();
            //_PackageManager = new PackageManager(_ProjectManager);
            _objectManager = new ObjectManager();
            _screenManager = new ScreenManager();

            _uiManager = new UserInterfaceManager();

#if EDITOR
            _externalToolManager = new ExternalToolManager();
            _assetManager = new AssetManager();
            _effect = null;
#endif
        }
    }
}
