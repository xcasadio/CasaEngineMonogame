using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal sealed class CasaRenderTargetService
{
    private readonly CasaDrawTransaction _owner;

    public CasaRenderTargetService(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public void SetRenderTarget(RenderTarget2D renderTarget, Color? clearColor)
    {
        RenderTarget2D currentTarget = GetCurrentRenderTarget();
        if (ReferenceEquals(renderTarget, currentTarget))
        {
            return;
        }

        _owner.EndCurrentContext();
        _owner.GraphicsDevice.SetRenderTarget(renderTarget);
        if (clearColor.HasValue)
        {
            _owner.GraphicsDevice.Clear(clearColor.Value);
        }
    }

    public IDisposable SetRenderTargetTemporary(RenderTarget2D renderTarget, Color? clearColor)
        => new TemporaryChange<RenderTarget2D, Color?>(GetCurrentRenderTarget(), renderTarget, null, clearColor, SetRenderTarget);

    private RenderTarget2D GetCurrentRenderTarget()
    {
        RenderTargetBinding[] bindings = _owner.GraphicsDevice.GetRenderTargets();
        return bindings.Length == 0 ? null : bindings[0].RenderTarget as RenderTarget2D;
    }
}