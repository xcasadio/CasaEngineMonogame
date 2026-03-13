using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Project;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.GUI.MGUI;

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

    public IUIViewRuntimeFactory UIViewRuntimeFactory { get; set; }

    public IUICompositionService UICompositionService { get; set; }

    public EngineRuntimeContext(
        ProjectSettings projectSettings,
        string projectPath,
        Func<Guid, AssetInfo?> resolveAssetInfo,
        IUIViewRuntimeFactory? uiViewRuntimeFactory = null,
        IUICompositionService? uiCompositionService = null)
    {
        ProjectSettings = projectSettings;
        ProjectPath = projectPath;
        ResolveAssetInfo = resolveAssetInfo;
        UIViewRuntimeFactory = uiViewRuntimeFactory ?? new MguiViewRuntimeFactory();
        UICompositionService = uiCompositionService ?? DefaultUICompositionService.Instance;
    }

    public static EngineRuntimeContext FromGlobals()
    {
        return new EngineRuntimeContext(
            GameSettings.ProjectSettings,
            EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath),
            AssetCatalog.Get);
    }
}