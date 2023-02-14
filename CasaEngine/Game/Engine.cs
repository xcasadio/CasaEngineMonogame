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
        private Microsoft.Xna.Framework.Game _game;

        public static Engine Instance { get; } = new();

        internal bool ResetDevice { get; set; }
        public GraphicsDeviceManager GraphicsDeviceManager => (GraphicsDeviceManager)GameHelper.GetService<IGraphicsDeviceManager>(_game);
        public GraphicsProfile GraphicsProfile => _game.GraphicsDevice.GraphicsProfile;
        public AssetContentManager AssetContentManager { get; internal set; }
        public Asset2DManager Asset2DManager { get; }
        public ProjectSettings ProjectSettings { get; }
        public ProjectManager ProjectManager { get; }
        public ObjectManager ObjectManager { get; }
        public ScreenManager ScreenManager { get; }
        public UserInterfaceManager UiManager { get; }
        public int MultiSampleQuality { get; set; }
        public SpriteBatch SpriteBatch { get; set; }
        public ObjectRegistry ObjectRegistry { get; }
        public string[] Arguments { get; set; }
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

        public SpriteFont DefaultSpriteFont { get; set; }

#endif

#if EDITOR

        public BasicEffect BasicEffect { get; set; }

        public AssetManager AssetManager { get; }

        public ExternalToolManager ExternalToolManager { get; }

#endif

        private Engine()
        {
            _game = null;
            DefaultSpriteFont = null;
            SpriteBatch = null;
            Asset2DManager = new Asset2DManager();
            ProjectSettings = new ProjectSettings();
            ObjectRegistry = new ObjectRegistry();
            ProjectManager = new ProjectManager();
            ObjectManager = new ObjectManager();
            ScreenManager = new ScreenManager();

            UiManager = new UserInterfaceManager();

#if EDITOR
            ExternalToolManager = new ExternalToolManager();
            AssetManager = new AssetManager();
            BasicEffect = null;
#endif
        }
    }
}
