namespace CasaEngine.Framework.Rendering.Depth;

/// <summary>
/// Per-draw blend mode for the sorted sprite path of <see cref="Application.Components.SpriteRendererComponent"/>.
/// Carried alongside the queued sprite data (defaulting to <see cref="Opaque"/> for every legacy submission)
/// and applied as a device state change on each contiguous run of same-mode sprites in the key-sorted list.
/// </summary>
public enum SpriteBlendMode
{
    /// <summary>The existing fixed opaque state (ColorSourceBlend = One, ColorDestinationBlend = Zero).</summary>
    Opaque = 0,

    /// <summary>
    /// <see cref="Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied"/>
    /// (SourceAlpha / InverseSourceAlpha).
    /// The sprite pixel shader outputs texel * Color without premultiplying alpha, so this is the blend
    /// state that matches the shader's output convention: with a vertex alpha of 0.5 it yields exactly
    /// 0.5 * src + 0.5 * dest (the PSX "Average" blend mode). The premultiplied equivalent
    /// (Blend.One / Blend.InverseSourceAlpha) would instead give src + 0.5 * dest against a
    /// non-premultiplied source, which is wrong for this shader.
    /// </summary>
    AlphaBlend = 1,

    /// <summary>
    /// PSX GPU additive blend (<c>dst' = dst + src</c>, per channel, saturated): <c>ColorBlendFunction
    /// = Add</c>, <c>ColorSourceBlend = One</c>, <c>ColorDestinationBlend = One</c>. Alpha uses
    /// <c>Add</c>, <c>Zero</c>/<c>One</c> so the destination alpha is preserved (see
    /// <see cref="Application.Components.SpriteRendererComponent"/>'s cached state doc). Used by the
    /// screen fade/tint overlay and by backdrop layers tagged additive.
    /// </summary>
    Additive = 2,

    /// <summary>
    /// PSX GPU subtractive blend (<c>dst' = dst − src</c>, per channel, saturated at zero):
    /// <c>ColorBlendFunction = ReverseSubtract</c> (NOT <c>Subtract</c>, which would compute
    /// <c>src − dst</c>), <c>ColorSourceBlend = One</c>, <c>ColorDestinationBlend = One</c>. Alpha uses
    /// <c>Add</c>, <c>Zero</c>/<c>One</c>, same as <see cref="Additive"/>.
    /// </summary>
    Subtractive = 3
}
