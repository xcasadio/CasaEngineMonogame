namespace CasaEngine.Framework.Assets.Sprites;

/// <summary>
/// The PSX semi-transparency mode of a sprite (E19.g G2a, ADR-0051). A sprite that carries a mode other than
/// <see cref="None"/> is drawn as two disjoint passes by the sorted sprite path: its opaque texels (raw alpha above
/// 0.75) with the opaque blend state, then its semi-transparent texels (raw alpha in 0.25 to 0.75, the PSX "STP" texels
/// of the sheet) with the blend state of the mode. Modes 0 to 3 are the four values of the PSX GPU's semi-transparency
/// rate (ABR).
/// </summary>
public enum SpritePsxSemiTransparency
{
    /// <summary>The sprite is not semi-transparent: one draw, every non-transparent texel opaque.</summary>
    None = 0,

    /// <summary>ABR 0, average: <c>0.5 * back + 0.5 * front</c> (non-premultiplied alpha blend at alpha 128).</summary>
    Mode0 = 1,

    /// <summary>ABR 1, additive: <c>back + front</c>, saturated.</summary>
    Mode1 = 2,

    /// <summary>ABR 2, subtractive: <c>back - front</c>, saturated at zero.</summary>
    Mode2 = 3,

    /// <summary>ABR 3, quarter: <c>back + 0.25 * front</c>, drawn additively with the colour (64, 64, 64).</summary>
    Mode3 = 4
}
