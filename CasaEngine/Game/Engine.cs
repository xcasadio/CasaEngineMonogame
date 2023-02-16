using CasaEngine.Assets;
using CasaEngine.Core.Game;
using CasaEngine.FrontEnd.Screen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Graphics2D;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Gameplay;
using CasaEngine.Physics2D;
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
        public Microsoft.Xna.Framework.Game Game { get; set; }
        public ProjectSettings ProjectSettings { get; } = new();
        public PhysicsSettings PhysicsSettings { get; } = new();
        public GraphicsSettings GraphicsSettings { get; } = new();

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
