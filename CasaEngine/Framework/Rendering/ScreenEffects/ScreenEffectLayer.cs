namespace CasaEngine.Framework.Rendering.ScreenEffects;

/// <summary>
/// Where a <see cref="ScreenEffectService"/> overlay draws relative to the composed MGUI interface.
/// See docs/decisions (above-UI screen effect layer ADR) for the rationale.
/// </summary>
public enum ScreenEffectLayer
{
    /// <summary>Default: the overlay draws before the UI is composed, so the UI stays on top and
    /// unaffected by the overlay colour. No existing consumer changes behaviour.</summary>
    BelowUI,

    /// <summary>The overlay draws after the UI is composed, so a fade darkens the UI along with the
    /// scene (the Alundra warp fade darkening its life gauge, for instance).</summary>
    AboveUI,
}
