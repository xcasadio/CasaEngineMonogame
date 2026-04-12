using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Assets;

public class CasaMonoGameImageResource : IUIImageResource
{
    public Texture2D Texture { get; }
    public int Width => Texture.Width;
    public int Height => Texture.Height;
    public bool IsDisposed => Texture.IsDisposed;

    public CasaMonoGameImageResource(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Texture = texture;
    }
}

public sealed class CasaMonoGameRenderTarget : CasaMonoGameImageResource, IUIRenderTarget
{
    public RenderTarget2D RenderTarget { get; }

    public CasaMonoGameRenderTarget(RenderTarget2D renderTarget)
        : base(renderTarget)
    {
        ArgumentNullException.ThrowIfNull(renderTarget);
        RenderTarget = renderTarget;
    }
}
