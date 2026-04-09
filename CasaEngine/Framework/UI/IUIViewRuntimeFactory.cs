using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Creates the concrete UI runtime hosted by a render view.
/// </summary>
public interface IUIViewRuntimeFactory
{
    IUIViewRuntime Create(CasaEngineGame game, IRenderSurface surface, EngineRuntimeContext runtimeContext);
}