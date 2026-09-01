namespace CasaEngine.Framework.Rendering.Depth;

public enum RenderPass2D
{
    Background = 0,
    Ground = 100,
    GroundDetails = 200,
    YSortedWorld = 300,
    Foreground = 400,
    Effects = 500,

    /// <summary>
    /// The full-viewport screen fade/tint overlay (see
    /// <see cref="Application.Components.ScreenEffectComponent"/>), above every world/effects layer
    /// and below the UI.
    /// </summary>
    ScreenEffects = 750,

    UI = 1000
}
