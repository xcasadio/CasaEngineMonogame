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
    /// and, by default, below the UI. A <see cref="ScreenEffects.ScreenEffectService"/> whose
    /// <see cref="ScreenEffects.ScreenEffectService.Layer"/> is set to
    /// <see cref="ScreenEffects.ScreenEffectLayer.AboveUI"/> still submits at this same pass, but the
    /// quad is drawn after the UI composition step instead, as a post-UI overlay (ADR-0033).
    /// </summary>
    ScreenEffects = 750,

    UI = 1000
}
