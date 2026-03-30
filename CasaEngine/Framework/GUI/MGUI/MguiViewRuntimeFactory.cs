using CasaEngine.Framework.Game;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Default UI runtime factory backed by MGUI.
/// </summary>
public sealed class MguiViewRuntimeFactory : IUIViewRuntimeFactory
{
    public IUIViewRuntime Create(CasaEngineGame game, IRenderSurface surface, EngineRuntimeContext runtimeContext)
    => new UIRoot(game, surface, runtimeContext);
}