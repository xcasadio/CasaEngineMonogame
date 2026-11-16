using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Clipping;

internal sealed class CasaClipManager
{
    private readonly CasaDrawTransaction _Owner;
    private readonly ClipBackendCapabilities _Capabilities;
    private readonly CasaScissorClipExecutor _ScissorExecutor;
    private readonly CasaStencilClipExecutor _StencilExecutor;
    private readonly CasaMaskClipExecutor _MaskExecutor;
    private int _ScissorClipCount;
    private int _StencilClipCount;
    private int _MaskClipCount;

    public CasaClipManager(CasaDrawTransaction owner)
    {
        _Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _Capabilities = ClipBackendCapabilities.Default;
        _ScissorExecutor = new CasaScissorClipExecutor(owner);
        _StencilExecutor = new CasaStencilClipExecutor(owner);
        _MaskExecutor = new CasaMaskClipExecutor(owner);
    }

    /// <summary>Puts the manager in the state its constructor gives it: the counts and the depths the diagnostics report are back to zero.
    /// The clips in force are forgotten by the transaction, which owns their slots.</summary>
    public void Rearm()
    {
        _ScissorClipCount = 0;
        _StencilClipCount = 0;
        _MaskClipCount = 0;
        _StencilExecutor.Rearm();
        _MaskExecutor.Rearm();
    }

    public ClipResolveResult Resolve(ClipDefinition definition)
        => ClipStrategyResolver.Resolve(definition, _Capabilities);

    public ClipDiagnosticsSnapshot GetDiagnostics()
        => new(_ScissorClipCount, _StencilClipCount, _MaskClipCount, _StencilExecutor.MaxObservedDepth,
            _MaskExecutor.TemporaryRenderTargetRentCount, _MaskExecutor.TemporaryRenderTargetReuseCount);

    public ClipHandle Push(ClipDefinition definition)
    {
        ClipResolveResult resolution = Resolve(definition);

        return resolution.Strategy switch
        {
            ClipStrategy.None => PushNoClip(resolution),
            ClipStrategy.Scissor => PushScissor(resolution),
            ClipStrategy.Stencil => PushStencil(resolution),
            ClipStrategy.Mask => PushMask(resolution),
            _ => throw new NotSupportedException($"Clip strategy '{resolution.Strategy}' is not available until the corresponding backend is installed.")
        };
    }

    /// <summary>Pushes the clip that <see cref="Push(ClipDefinition)"/> pushes for <see cref="ClipDefinition.Rectangle"/> (default strategy preference, scissor
    /// available), counted the same, without creating the definition.</summary>
    public ClipHandle PushRectangle(Rectangle bounds, bool intersectWithCurrentClip)
    {
        _ScissorClipCount++;
        return _Owner.PushRectangleClipCore(bounds, intersectWithCurrentClip);
    }

    private ClipHandle PushNoClip(ClipResolveResult resolution)
    {
        return _Owner.ClipSlots.Push(ClipSlotState.None(resolution));
    }

    private ClipHandle PushScissor(ClipResolveResult resolution)
    {
        _ScissorClipCount++;
        return _ScissorExecutor.Push(resolution);
    }

    private ClipHandle PushStencil(ClipResolveResult resolution)
    {
        _StencilClipCount++;
        return _StencilExecutor.Push(resolution);
    }

    private ClipHandle PushMask(ClipResolveResult resolution)
    {
        _MaskClipCount++;
        return _MaskExecutor.Push(resolution);
    }
}
