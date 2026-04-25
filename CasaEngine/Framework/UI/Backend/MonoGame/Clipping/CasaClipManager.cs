using MGUI.Shared.Rendering.Clipping;

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
            ClipStrategy.None => new(resolution, () => { }),
            ClipStrategy.Scissor => PushScissor(resolution),
            ClipStrategy.Stencil => PushStencil(resolution),
            ClipStrategy.Mask => PushMask(resolution),
            _ => throw new NotSupportedException($"Clip strategy '{resolution.Strategy}' is not available until the corresponding backend is installed.")
        };
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
}