using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Helpers;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

internal readonly record struct CasaRenderTargetLease(RenderTarget2D Target, bool WasReused);

internal readonly record struct CasaRenderTargetPoolKey(
    int Width,
    int Height,
    SurfaceFormat SurfaceFormat,
    DepthFormat DepthFormat,
    int MultiSampleCount,
    RenderTargetUsage Usage);

internal sealed class CasaRenderTargetPool
{
    private const int MaxTotalPooledTargets = 16;
    private const int MaxTargetsPerKey = 4;

    private readonly Dictionary<CasaRenderTargetPoolKey, Stack<RenderTarget2D>> _available = new();
    private int _totalPooledTargets;

    public CasaRenderTargetLease Rent(GraphicsDevice graphicsDevice, int width, int height, bool preserveContents)
    {
        CasaRenderTargetPoolKey key = CreateKey(width, height, preserveContents);
        if (_available.TryGetValue(key, out Stack<RenderTarget2D>? stack))
        {
            while (stack.Count > 0)
            {
                RenderTarget2D target = stack.Pop();
                _totalPooledTargets--;
                if (!target.IsDisposed)
                {
                    return new CasaRenderTargetLease(target, true);
                }
            }
        }

        return new CasaRenderTargetLease(RenderUtils.CreateRenderTarget(graphicsDevice, width, height, preserveContents), false);
    }

    public void Return(RenderTarget2D renderTarget)
    {
        if (renderTarget == null || renderTarget.IsDisposed)
        {
            return;
        }

        CasaRenderTargetPoolKey key = CreateKey(renderTarget.Width, renderTarget.Height,
            renderTarget.RenderTargetUsage == RenderTargetUsage.PreserveContents);
        if (!_available.TryGetValue(key, out Stack<RenderTarget2D>? stack))
        {
            stack = new Stack<RenderTarget2D>();
            _available[key] = stack;
        }

        if (stack.Count >= MaxTargetsPerKey || _totalPooledTargets >= MaxTotalPooledTargets)
        {
            renderTarget.Dispose();
            return;
        }

        stack.Push(renderTarget);
        _totalPooledTargets++;
    }

    private static CasaRenderTargetPoolKey CreateKey(int width, int height, bool preserveContents)
        => new(
            width,
            height,
            SurfaceFormat.Color,
            DepthFormat.Depth24,
            0,
            preserveContents ? RenderTargetUsage.PreserveContents : RenderTargetUsage.DiscardContents);
}