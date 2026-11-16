using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal sealed class CasaRenderTargetService : IDrawStateHandleOwner
{
    private readonly CasaDrawTransaction _owner;
    // The render target each temporary change in force restores, one slot per change (MGUI ADR-0031).
    private readonly GenerationSlots<RenderTarget2D> _temporaryTargets = new();

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

    public DrawStateHandle SetRenderTargetTemporary(RenderTarget2D renderTarget, Color? clearColor)
    {
        int slot = _temporaryTargets.Acquire(GetCurrentRenderTarget(), out int generation);
        SetRenderTarget(renderTarget, clearColor);
        return new DrawStateHandle(this, slot, generation);
    }

    public void ReleaseDrawState(int slot, int generation)
    {
        if (_temporaryTargets.TryRelease(slot, generation, out RenderTarget2D previous))
        {
            SetRenderTarget(previous, null);
        }
    }

    /// <summary>Ends every temporary change without restoring anything: the handles of the previous use of the transaction restore nothing.</summary>
    public void Rearm() => _temporaryTargets.ReleaseAll();

    private readonly RenderTargetBinding[] _singleBinding = new RenderTargetBinding[1];

    /// <summary>The first render target bound to the device, or null for the back buffer. Read without the array that
    /// <see cref="GraphicsDevice.GetRenderTargets()"/> creates when one target is bound, which is the case of every target this service sets.</summary>
    private RenderTarget2D GetCurrentRenderTarget()
    {
        GraphicsDevice device = _owner.GraphicsDevice;
        int count = device.RenderTargetCount;
        if (count == 0)
        {
            return null;
        }

        if (count == 1)
        {
            device.GetRenderTargets(_singleBinding);
            RenderTarget2D target = _singleBinding[0].RenderTarget as RenderTarget2D;
            _singleBinding[0] = default;
            return target;
        }

        return device.GetRenderTargets()[0].RenderTarget as RenderTarget2D;
    }
}
