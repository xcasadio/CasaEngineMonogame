using CasaEngine.Editor.Tools;
using CasaEngine.Engine.Physics2D;
using CasaEngine.Engine.Plugin;
using CasaEngine.Framework.Project;

namespace CasaEngine.Framework.Game;

public class GameSettings
{
    public static ProjectManager ProjectManager { get; } = new();
    public static PluginManager PluginManager { get; } = new();

    public static ProjectSettings ProjectSettings { get; } = new();
    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static Physics2dSettings Physics2dSettings { get; } = new();
    public static Physics3dSettings Physics3dSettings { get; } = new();

#if EDITOR
    public static ExternalToolManager ExternalToolManager { get; } = new();
#endif
}