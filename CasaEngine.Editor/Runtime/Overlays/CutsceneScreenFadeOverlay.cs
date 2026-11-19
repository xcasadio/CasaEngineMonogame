using System;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime.Overlays;

/// <summary>
/// Draws the screen fade or tint of a cutscene preview on top of the preview image, with the blend of the engine
/// (ADR-0103). It is registered on the render view of the preview viewport as a post-UI overlay and reads a
/// <see cref="ScreenEffectService"/>; it never changes it.
/// </summary>
/// <remarks>
/// The veil is one white pixel stretched over the whole current viewport of the device, in screen coordinates: the preview
/// camera is not a 2D camera, so the formula of the game does not apply. The blend states below are copies of the private
/// ones of <c>SpriteRendererComponent</c> (a test compares them field by field). Every device state the draw changes is
/// restored.
/// </remarks>
public sealed class CutsceneScreenFadeOverlay : IPostUIOverlay, IDisposable
{
    /// <summary>A veil of this alpha or less is not drawn: the sprite shader of the engine discards those texels.</summary>
    public const byte MaxInvisibleAlpha = 2;

    // Copy of the fixed opaque state of SpriteRendererComponent (_blendState): colour One/Zero, alpha Max.
    internal static readonly BlendState OpaqueBlendState = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        AlphaBlendFunction = BlendFunction.Max,
        ColorSourceBlend = Blend.One,
        AlphaSourceBlend = Blend.SourceColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.DestinationAlpha
    };

    // Copy of SpriteRendererComponent.AdditiveBlendState: the colour saturates through One/One, the destination alpha is kept.
    internal static readonly BlendState AdditiveBlendState = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };

    // Copy of SpriteRendererComponent.SubtractiveBlendState: ReverseSubtract computes dst - src, like the subtractive mode
    // of the original console GPU.
    internal static readonly BlendState SubtractiveBlendState = new()
    {
        ColorBlendFunction = BlendFunction.ReverseSubtract,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };

    private readonly ScreenEffectService _effect;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;

    public CutsceneScreenFadeOverlay(ScreenEffectService effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _effect = effect;
    }

    /// <summary>How many times the veil has been drawn.</summary>
    public int DrawCount { get; private set; }

    /// <summary>True when the effect is active and visible enough to be drawn.</summary>
    public static bool ShouldDraw(ScreenEffectService effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect.Active && effect.A > MaxInvisibleAlpha;
    }

    /// <summary>The blend state of the engine for a blend mode, the same one for the same mode.</summary>
    public static BlendState GetBlendState(SpriteBlendMode blendMode)
    {
        return blendMode switch
        {
            SpriteBlendMode.AlphaBlend => BlendState.NonPremultiplied,
            SpriteBlendMode.Additive => AdditiveBlendState,
            SpriteBlendMode.Subtractive => SubtractiveBlendState,
            _ => OpaqueBlendState
        };
    }

    public void Draw(GraphicsDevice graphicsDevice, RenderView view, in RenderFrame frame)
    {
        if (!ShouldDraw(_effect))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(graphicsDevice);

        // The size comes from the viewport bound at this moment: with shadows on, the bound target is an intermediate one.
        var viewport = graphicsDevice.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        if (_spriteBatch == null)
        {
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _pixel = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
            _pixel.SetData(new[] { Color.White });
        }

        using var guard = new GraphicsStateGuard(graphicsDevice);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            GetBlendState(_effect.Blend),
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(_effect.R, _effect.G, _effect.B, _effect.A));
        _spriteBatch.End();

        DrawCount++;
    }

    public void Dispose()
    {
        _spriteBatch?.Dispose();
        _spriteBatch = null;
        _pixel?.Dispose();
        _pixel = null;
    }
}
