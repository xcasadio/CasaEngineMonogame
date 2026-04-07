using CasaEngine.Framework.Rendering.Draw;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Default forward-lighting 3-D render pipeline.
/// Executes passes in order: Sky → Opaque → Transparent.
///
/// To extend the pipeline (e.g. add a depth pre-pass, shadow pass or post-process pass)
/// insert additional <see cref="RenderPass"/> instances via <see cref="InsertPass"/> /
/// <see cref="AddPass"/>.
/// </summary>
public sealed class ForwardRenderPipeline : IRenderPipeline3D
{
    private readonly List<RenderPass> _passes = new();
    private readonly SkyPass _skyPass = new();

    // -----------------------------------------------------------------------
    //  Constructor
    // -----------------------------------------------------------------------

    public ForwardRenderPipeline()
    {
        _passes.Add(_skyPass);
        _passes.Add(new OpaquePass());
        _passes.Add(new TransparentPass());
    }

    public void SetSkyRenderer(Environment.SkyCubemapRenderer? skyRenderer)
    {
        _skyPass.Renderer = skyRenderer;
    }

    // -----------------------------------------------------------------------
    //  Pass management
    // -----------------------------------------------------------------------

    /// <summary>Appends a pass to the end of the execution order.</summary>
    public void AddPass(RenderPass pass) => _passes.Add(pass);

    /// <summary>Inserts a pass before the first existing pass of the specified type.</summary>
    public void InsertPass(RenderPassType before, RenderPass pass)
    {
        int idx = _passes.FindIndex(p => p.Type == before);
        if (idx < 0)
        {
            _passes.Add(pass);
        }
        else
        {
            _passes.Insert(idx, pass);
        }
    }

    /// <summary>Removes all passes of the given type.</summary>
    public void RemovePass(RenderPassType type) =>
        _passes.RemoveAll(p => p.Type == type);

    // -----------------------------------------------------------------------
    //  IRenderPipeline3D
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Initialize(GraphicsDevice device)
    {
        // Nothing to initialise for the basic forward pipeline.
        // Concrete passes can override this if they need GPU resources.
    }

    /// <inheritdoc/>
    public void Render(
        RenderContext context,
        IReadOnlyList<RenderItem> items,
        RenderStateCache stateCache,
        ShaderBindCache shaderCache,
        RenderShaderSelector shaderSelector)
    {
        foreach (var pass in _passes)
            pass.Execute(context, items, stateCache, shaderCache, shaderSelector);
    }
}
