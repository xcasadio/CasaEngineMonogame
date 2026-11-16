using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.UI.Backend.MonoGame.Clipping;

internal sealed class CasaMaskClipExecutor
{
    private readonly CasaDrawTransaction _owner;

    public CasaMaskClipExecutor(CasaDrawTransaction owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public int TemporaryRenderTargetRentCount { get; private set; }
    public int TemporaryRenderTargetReuseCount { get; private set; }

    /// <summary>Puts the executor in the state its constructor gives it. The render target that a mask clip left open rented is not returned
    /// to the pool, as when the transaction that pushed that clip is dropped: only its closure holds it.</summary>
    public void Rearm()
    {
        TemporaryRenderTargetRentCount = 0;
        TemporaryRenderTargetReuseCount = 0;
    }

    public ClipScope Push(ClipResolveResult resolution)
    {
        ClipGeometry geometry = resolution.Effective.Shape.Geometry ?? throw new InvalidOperationException(
            $"Clip '{resolution.Effective.DebugName ?? resolution.Effective.Kind.ToString()}' requires clip geometry for mask rendering.");
        Rectangle bounds = resolution.Effective.Shape.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new ClipScope(resolution, () => { });
        }

        CasaRenderTargetLease renderTargetLease = _owner.Renderer.RenderTargetPool.Rent(_owner.GraphicsDevice, bounds.Width, bounds.Height, false);
        RenderTarget2D maskTarget = renderTargetLease.Target;
        TemporaryRenderTargetRentCount++;
        if (renderTargetLease.WasReused)
        {
            TemporaryRenderTargetReuseCount++;
        }

        DrawStateHandle depthStencilDisableScope = _owner.SetDrawSettingsTemporary(_owner.CurrentSettings with
        {
            DepthStencilType = DepthStencilType.None,
        });
        DrawStateHandle renderTargetScope = _owner.SetRenderTargetTemporary(maskTarget, Color.Transparent);
        ClipScope clipDisableScope = _owner.PushRectangleClip(null, false);
        DrawStateHandle transformScope = _owner.SetTransformTemporary(_owner.CurrentSettings.Transform * Matrix.CreateTranslation(-bounds.Left, -bounds.Top, 0));

        using (_owner.SetDrawSettingsTemporary(_owner.CurrentSettings with
        {
            BlendType = BlendType.Opaque,
            DepthStencilType = DepthStencilType.None,
        }))
        {
            for (int i = 0; i + 2 < geometry.Indices.Count; i += 3)
            {
                Vector2 v0 = geometry.Vertices[geometry.Indices[i]];
                Vector2 v1 = geometry.Vertices[geometry.Indices[i + 1]];
                Vector2 v2 = geometry.Vertices[geometry.Indices[i + 2]];
                _owner.FillTriangle(Vector2.Zero, v0, Color.Black, v1, Color.Black, v2, Color.Black);
            }
        }

        DrawStateHandle maskedContentScope = _owner.SetDrawSettingsTemporary(_owner.CurrentSettings with
        {
            BlendType = BlendType.DestinationAlphaMask,
            DepthStencilType = DepthStencilType.None,
        });

        return new ClipScope(resolution, () =>
        {
            maskedContentScope.Dispose();
            transformScope.Dispose();
            renderTargetScope.Dispose();
            clipDisableScope.Dispose();
            depthStencilDisableScope.Dispose();

            _owner.DrawTextureTo(maskTarget, null, bounds);
            _owner.Renderer.RenderTargetPool.Return(maskTarget);
        });
    }
}