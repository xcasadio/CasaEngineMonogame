using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// An immutable snapshot of the GPU render state, used by <see cref="GraphicsStateGuard"/>
/// to restore the device to a known state between render passes.
/// </summary>
public readonly struct GraphicsStateSnapshot
{
    public RenderTargetBinding[] RenderTargets    { get; init; }
    public Viewport              Viewport          { get; init; }
    public Rectangle             ScissorRectangle  { get; init; }
    public BlendState            BlendState        { get; init; }
    public DepthStencilState     DepthStencilState { get; init; }
    public RasterizerState       RasterizerState   { get; init; }
    public SamplerState          Sampler0          { get; init; }
    public SamplerState          Sampler1          { get; init; }
    public SamplerState          Sampler2          { get; init; }

    /// <summary>
    /// Captures the current render state of <paramref name="gd"/>.
    /// </summary>
    public static GraphicsStateSnapshot Capture(GraphicsDevice gd) =>
        new()
        {
            RenderTargets     = gd.GetRenderTargets(),
            Viewport          = gd.Viewport,
            ScissorRectangle  = gd.ScissorRectangle,
            BlendState        = gd.BlendState,
            DepthStencilState = gd.DepthStencilState,
            RasterizerState   = gd.RasterizerState,
            Sampler0          = gd.SamplerStates[0],
            Sampler1          = gd.SamplerStates[1],
            Sampler2          = gd.SamplerStates[2],
        };

    /// <summary>
    /// Restores the captured state onto <paramref name="gd"/>.
    /// </summary>
    public void Restore(GraphicsDevice gd)
    {
        // Restore the render targets first — some state (e.g. Viewport) is implicitly
        // overwritten by SetRenderTargets on certain platforms.
        if (RenderTargets.Length == 0)
        {
            gd.SetRenderTarget(null);
        }
        else
        {
            gd.SetRenderTargets(RenderTargets);
        }

        gd.Viewport          = Viewport;
        gd.ScissorRectangle  = ScissorRectangle;
        gd.BlendState        = BlendState;
        gd.DepthStencilState = DepthStencilState;
        gd.RasterizerState   = RasterizerState;
        gd.SamplerStates[0]  = Sampler0;
        gd.SamplerStates[1]  = Sampler1;
        gd.SamplerStates[2]  = Sampler2;
    }
}
