using CasaEngine.Editor.Tools;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Project;

namespace CasaEngine.Framework.Game;

public class GameSettings
{
    public static ProjectSettings ProjectSettings { get; } = new();
    public static PluginManager PluginManager { get; } = new();

    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static PhysicsEngineSettings PhysicsEngineSettings { get; } = new();

#if EDITOR
    public static ExternalToolManager ExternalToolManager { get; } = new();
#endif
}