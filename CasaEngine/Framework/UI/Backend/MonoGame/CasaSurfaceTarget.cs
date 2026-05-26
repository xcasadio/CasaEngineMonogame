using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal enum CasaSurfaceTargetKind
{
    BackBuffer,
    RenderTarget,
}

internal readonly record struct CasaSurfaceTargetDescriptor(
    CasaSurfaceTargetKind Kind,
    Rectangle Bounds,
    IUIRenderTarget RenderTarget)
{
    public bool IsBackBuffer => Kind == CasaSurfaceTargetKind.BackBuffer;

    public static CasaSurfaceTargetDescriptor CreateBackBuffer(Rectangle bounds)
        => new(CasaSurfaceTargetKind.BackBuffer, bounds, null);

    public static CasaSurfaceTargetDescriptor CreateRenderTarget(Rectangle bounds, IUIRenderTarget renderTarget)
    {
        ArgumentNullException.ThrowIfNull(renderTarget);
        return new CasaSurfaceTargetDescriptor(CasaSurfaceTargetKind.RenderTarget, bounds, renderTarget);
    }
}

internal interface ICasaSurfaceTargetProvider
{
    CasaSurfaceTargetDescriptor GetTargetDescriptor();
}