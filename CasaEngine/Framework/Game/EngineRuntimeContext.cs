using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.GUI;
using CasaEngine.Framework.Input;
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

    public Func<string, AssetInfo?> ResolveAssetInfoByFileName { get; set; }

    public RenderTargetPool? RenderTargetPool { get; set; }

    public IUIViewRuntimeFactory UIViewRuntimeFactory { get; set; }

    public IUICompositionService UICompositionService { get; set; }

    public IWindowInputSource? WindowInputSource { get; set; }

    public EngineRuntimeContext(
        ProjectSettings projectSettings,
        string projectPath,
        Func<Guid, AssetInfo?> resolveAssetInfo,
        Func<string, AssetInfo?>? resolveAssetInfoByFileName = null,
        IUIViewRuntimeFactory? uiViewRuntimeFactory = null,
        IUICompositionService? uiCompositionService = null)
    {
        ProjectSettings = projectSettings;
        ProjectPath = projectPath;
        ResolveAssetInfo = resolveAssetInfo;
        ResolveAssetInfoByFileName = resolveAssetInfoByFileName ?? AssetCatalog.GetByFileName;
        UIViewRuntimeFactory = uiViewRuntimeFactory ?? new MguiViewRuntimeFactory();
        UICompositionService = uiCompositionService ?? DefaultUICompositionService.Instance;
    }

    public static EngineRuntimeContext FromGlobals()
    {
        return new EngineRuntimeContext(
            GameSettings.ProjectSettings,
            EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath),
            AssetCatalog.Get,
            AssetCatalog.GetByFileName);
    }

    public string GetAssetPath(string relativeFileName)
    {
        return Path.Combine(ProjectPath, relativeFileName);
    }
}