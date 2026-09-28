using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Plugins;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.SaveGames;

namespace CasaEngine.Framework.Application;

public static class GameSettings
{
    public static ProjectSettings ProjectSettings { get; } = new();
    public static AssemblyManager AssemblyManager { get; } = new();
    public static GraphicsSettings GraphicsSettings { get; } = new();
    public static PhysicsEngineSettings PhysicsEngineSettings { get; } = new();

    /// <summary>
    /// The game's save-game slots (ADR-0044). Construction touches no disk: the per-user folder is resolved from
    /// <see cref="ProjectSettings"/> at the first save-game I/O call.
    /// </summary>
    public static SaveGameService SaveGames { get; } = new();

    public static EngineRuntimeContext CreateRuntimeContext() => EngineRuntimeContext.FromGlobals();
}