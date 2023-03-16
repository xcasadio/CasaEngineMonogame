using CasaEngine.Editor.Assets;
using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Gameplay.Actor;
using CasaEngine.Framework.UserInterface;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Engine.Physics2D;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.Project;
#if EDITOR
using CasaEngine.Editor.Tools;
#endif

namespace CasaEngine.Framework.Game
{
    public class Engine
    {
        public static Engine Instance { get; } = new();

        public string[] Arguments { get; set; }
        public GraphicsDeviceManager GraphicsDeviceManager => (GraphicsDeviceManager)Game.GetService<IGraphicsDeviceManager>();
        public AssetContentManager AssetContentManager { get; internal set; } = new();
        public Asset2DManager Asset2DManager { get; } = new();
        public ProjectManager ProjectManager { get; } = new();
        public ObjectManager ObjectManager { get; } = new();
        public ScreenManager ScreenManager { get; } = new();
        public UserInterfaceManager UiManager { get; } = new();
        public SpriteBatch SpriteBatch { get; set; }
        public ObjectRegistry ObjectRegistry { get; } = new();
        public CasaEngineGame Game { get; set; }
        public ProjectSettings ProjectSettings { get; } = new();
        public Physics2dSettings Physics2dSettings { get; } = new();
        public GraphicsSettings GraphicsSettings { get; } = new();

#if !FINAL

        public SpriteFont DefaultSpriteFont { get; set; }

#endif

#if EDITOR

        public BasicEffect BasicEffect { get; set; }

        public AssetManager AssetManager { get; }

        public ExternalToolManager ExternalToolManager { get; }
        public PluginManager PluginManager { get; } = new();

#endif

        private Engine()
        {
            Game = null;
            DefaultSpriteFont = null;
            SpriteBatch = null;

#if EDITOR
            ExternalToolManager = new ExternalToolManager();
            AssetManager = new AssetManager();
            BasicEffect = null;
#endif
        }
    }
}
