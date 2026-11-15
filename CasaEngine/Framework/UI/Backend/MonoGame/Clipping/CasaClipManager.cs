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
    private readonly Stack<NoClipSlot> _FreeNoClipSlots = new();
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
    /// The free slots are kept: a slot is only a place to build a scope, and one that a clip left open never comes back to it.</summary>
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

    public ClipScope Push(ClipDefinition definition)
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
    public ClipScope PushRectangle(Rectangle bounds, bool intersectWithCurrentClip)
    {
        _ScissorClipCount++;
        return _Owner.PushRectangleClipCore(bounds, intersectWithCurrentClip);
    }

    private ClipScope PushNoClip(ClipResolveResult resolution)
    {
        NoClipSlot slot = _FreeNoClipSlots.Count > 0 ? _FreeNoClipSlots.Pop() : new NoClipSlot(this);
        slot.Scope.Reset(resolution);
        return slot.Scope;
    }

    private ClipScope PushScissor(ClipResolveResult resolution)
    {
        _ScissorClipCount++;
        return _ScissorExecutor.Push(resolution);
    }

    private ClipScope PushStencil(ClipResolveResult resolution)
    {
        _StencilClipCount++;
        return _StencilExecutor.Push(resolution);
    }

    private ClipScope PushMask(ClipResolveResult resolution)
    {
        _MaskClipCount++;
        return _MaskExecutor.Push(resolution);
    }

    /// <summary>The scope of a clip that changes nothing, kept for reuse once disposed.</summary>
    private sealed class NoClipSlot
    {
        private readonly CasaClipManager _Owner;
        public readonly ClipScope Scope;

        public NoClipSlot(CasaClipManager owner)
        {
            _Owner = owner;
            Scope = new ClipScope(Release);
        }

        private void Release() => _Owner._FreeNoClipSlots.Push(this);
    }
}
