using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaRenderSurfaceAdapter : IUISurface, ICasaSurfaceTargetProvider
{
    private readonly IRenderSurface _surface;
    private RenderTarget2D? _cachedRenderTarget;
    private CasaMonoGameRenderTarget? _cachedAdapter;

    public CasaRenderSurfaceAdapter(IRenderSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
    }

    public Rectangle GetBounds()
    {
        Rectangle viewport = _surface.ViewportRect;
        return new Rectangle(0, 0, viewport.Width, viewport.Height);
    }

    private CasaSurfaceTargetDescriptor GetTargetDescriptor()
    {
        RenderTarget2D? renderTarget = _surface.RenderTarget;
        if (renderTarget == null)
        {
            return CasaSurfaceTargetDescriptor.CreateBackBuffer(GetBounds());
        }

        if (!ReferenceEquals(_cachedRenderTarget, renderTarget))
        {
            _cachedRenderTarget = renderTarget;
            _cachedAdapter = new CasaMonoGameRenderTarget(renderTarget);
        }

        return CasaSurfaceTargetDescriptor.CreateRenderTarget(GetBounds(), _cachedAdapter!);
    }

    CasaSurfaceTargetDescriptor ICasaSurfaceTargetProvider.GetTargetDescriptor() => GetTargetDescriptor();

    public IUIRenderTarget GetRenderTarget() => GetTargetDescriptor().RenderTarget!;
}