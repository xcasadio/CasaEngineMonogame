using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Default UI runtime factory backed by MGUI.
/// </summary>
public sealed class MguiViewRuntimeFactory : IUIViewRuntimeFactory
{
    public IUIViewRuntime Create(CasaEngineGame game, IRenderSurface surface, EngineRuntimeContext runtimeContext)
    => new UIRoot(game, surface, runtimeContext);
}