using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Configuration.Project;

namespace CasaEngine.Framework.Application;

public static class GameSettings
{
    public static ProjectSettings ProjectSettings { get; } = new();
    public static AssemblyManager AssemblyManager { get; } = new();
    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static PhysicsEngineSettings PhysicsEngineSettings { get; } = new();

    public static EngineRuntimeContext CreateRuntimeContext() => EngineRuntimeContext.FromGlobals();
}