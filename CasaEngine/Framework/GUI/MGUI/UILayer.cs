namespace CasaEngine.Framework.GUI;

/// <summary>
/// Drawing and update priority layers within a <see cref="ScreenStack"/>.
/// Screens on higher layers are drawn on top of and updated before lower ones.
/// </summary>
public enum UILayer
{
    /// <summary>Heads-up display: health bar, minimap, ammo counter, crosshair.</summary>
    HUD     = 0,

    /// <summary>In-game menus: pause menu, inventory, map screen.</summary>
    Menu    = 1,

    /// <summary>Modal dialogs: confirmation boxes, error alerts. Blocks lower layers.</summary>
    Modal   = 2,

    /// <summary>Tooltip overlays shown above modal dialogs.</summary>
    Tooltip = 3,

    /// <summary>Developer / debug overlays (frame stats, cheats UI, etc.).</summary>
    Debug   = 4,
}
