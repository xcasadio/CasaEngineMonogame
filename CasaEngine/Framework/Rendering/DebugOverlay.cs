using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Draws a per-view debug overlay with performance statistics.
///
/// Statistics shown:
/// <list type="bullet">
///   <item>Instantaneous FPS (frames per second).</item>
///   <item>View name and update mode.</item>
///   <item>View resolution (actual RT width × height).</item>
///   <item>Active RT count from <see cref="RenderTargetPool"/> (if available).</item>
/// </list>
///
/// Enable per view by setting <see cref="RenderView.ShowDebugOverlay"/> = true.
/// The overlay is drawn by <see cref="RenderPipeline"/> after each view's render pass.
/// </summary>
public sealed class DebugOverlay
{
    private const int Padding     = 6;
    private const int LineSpacing = 18;

    private readonly SpriteBatch   _spriteBatch;
    private readonly DynamicSpriteFont _font;
    private readonly Texture2D     _background;

    // FPS tracking
    private float _fpsAccum;
    private int   _fpsFrames;
    private float _fps;

    public DebugOverlay(SpriteBatch spriteBatch, FontSystem fontSystem)
    {
        _spriteBatch = spriteBatch;
        _font        = fontSystem.GetFont(13);

        // 1×1 semi-transparent background
        _background = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
        _background.SetData(new[] { new Color(0, 0, 0, 160) });
    }

    /// <summary>
    /// Draws the debug overlay for <paramref name="view"/> into the current render target.
    /// Call after the view's pipeline has finished (surface still bound).
    /// </summary>
    public void Draw(RenderView view, Rectangle viewportRect, float deltaSeconds)
    {
        // Update FPS counter
        _fpsAccum += deltaSeconds;
        _fpsFrames++;
        if (_fpsAccum >= 0.5f)
        {
            _fps       = _fpsFrames / _fpsAccum;
            _fpsAccum  = 0f;
            _fpsFrames = 0;
        }

        // Collect stat lines
        var pool      = RenderTargetPool.Shared;
        var lines     = new List<string>(6)
        {
            $"View: {(string.IsNullOrEmpty(view.Name) ? "unnamed" : view.Name)}",
            $"FPS: {_fps:F1}",
            $"Mode: {view.UpdateMode}",
            $"Resolution: {viewportRect.Width} × {viewportRect.Height}",
            $"Scale: {view.ResolutionScale:P0}",
        };

        if (pool != null)
        {
            lines.Add($"RT pool: {pool.TotalCount - pool.FreeCount} active / {pool.FreeCount} free");
        }

        // Background rect
        int bgW = 220;
        int bgH = Padding * 2 + lines.Count * LineSpacing;
        var bgRect = new Rectangle(
            viewportRect.X + Padding,
            viewportRect.Y + Padding,
            bgW, bgH);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            null, DepthStencilState.None, RasterizerState.CullNone);

        _spriteBatch.Draw(_background, bgRect, Color.White);

        for (int i = 0; i < lines.Count; i++)
        {
            var pos = new Vector2(
                bgRect.X + Padding,
                bgRect.Y + Padding + i * LineSpacing);
            _font.DrawText(_spriteBatch, lines[i], pos, Color.White);
        }

        _spriteBatch.End();
    }

    /// <summary>Disposes the 1×1 background texture.</summary>
    public void Dispose()
    {
        _background.Dispose();
    }
}
