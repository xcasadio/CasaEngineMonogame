using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Project;

namespace CasaEngine.Framework.Game;

public static class GameSettings
{
    public static ProjectSettings ProjectSettings { get; } = new();
    public static AssemblyManager AssemblyManager { get; } = new();
    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static PhysicsEngineSettings PhysicsEngineSettings { get; } = new();
}