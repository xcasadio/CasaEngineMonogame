using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Game;

/// <summary>
/// Explicit runtime context for project, asset and render-target services.
/// Keeps the engine compatible with legacy globals while allowing callers to inject dependencies.
/// </summary>
public sealed class EngineRuntimeContext
{
    public ProjectSettings ProjectSettings { get; }

    public string ProjectPath { get; set; }

    public Func<Guid, AssetInfo?> ResolveAssetInfo { get; set; }

    public RenderTargetPool? RenderTargetPool { get; set; }

    public EngineRuntimeContext(ProjectSettings projectSettings, string projectPath, Func<Guid, AssetInfo?> resolveAssetInfo)
    {
        ProjectSettings = projectSettings;
        ProjectPath = projectPath;
        ResolveAssetInfo = resolveAssetInfo;
    }

    public static EngineRuntimeContext FromGlobals()
    {
        return new EngineRuntimeContext(
            GameSettings.ProjectSettings,
            EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath),
            AssetCatalog.Get);
    }
}