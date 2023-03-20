using CasaEngine.Editor.Assets;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Physics2D;
using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Gameplay.Actor;
using CasaEngine.Framework.Graphics2D;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.UserInterface;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#if EDITOR
using CasaEngine.Editor.Tools;
#endif

namespace CasaEngine.Framework
{
    public static class EngineComponents
    {
        public static string[] Arguments { get; set; }
        public static GraphicsDeviceManager GraphicsDeviceManager => (GraphicsDeviceManager)Game.GetService<IGraphicsDeviceManager>();
        public static AssetContentManager AssetContentManager { get; internal set; } = new();
        public static Asset2DManager Asset2DManager { get; } = new();
        public static ProjectManager ProjectManager { get; } = new();
        public static ObjectManager ObjectManager { get; } = new();
        public static ScreenManager ScreenManager { get; } = new();
        public static UserInterfaceManager UiManager { get; } = new();
        public static SpriteBatch? SpriteBatch { get; set; }
        public static ObjectRegistry ObjectRegistry { get; } = new();
        public static CasaEngineGame? Game { get; set; }
        public static ProjectSettings ProjectSettings { get; } = new();
        public static GraphicsSettings GraphicsSettings { get; } = new();
        public static PhysicsEngine2d PhysicsEngine2d { get; } = new();
        public static PhysicsEngine PhysicsEngine { get; } = new();
        public static Physics2dSettings Physics2dSettings { get; } = new();
        public static Physics3dSettings Physics3dSettings { get; } = new();

#if !FINAL

        public static SpriteFont? DefaultSpriteFont { get; set; }

#endif

#if EDITOR

        public static BasicEffect? BasicEffect { get; set; }

        public static AssetManager AssetManager { get; } = new();

        public static ExternalToolManager ExternalToolManager { get; } = new();
        public static PluginManager PluginManager { get; } = new();

#endif
    }
}
