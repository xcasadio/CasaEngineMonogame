using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using Microsoft.Xna.Framework;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaBackBufferSurface : IUISurface, ICasaSurfaceTargetProvider
{
    private readonly IRenderHost _host;

    public CasaBackBufferSurface(IRenderHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public Rectangle GetBounds() => _host.GetBounds();

    private CasaSurfaceTargetDescriptor GetTargetDescriptor()
        => CasaSurfaceTargetDescriptor.CreateBackBuffer(GetBounds());

    CasaSurfaceTargetDescriptor ICasaSurfaceTargetProvider.GetTargetDescriptor() => GetTargetDescriptor();

    public IUIRenderTarget GetRenderTarget() => GetTargetDescriptor().RenderTarget!;
}