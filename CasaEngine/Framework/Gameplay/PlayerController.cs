using CasaEngine.Framework.UI;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Gameplay;

/**
 * PlayerControllers are used by human players to control Pawns.
 *
 * ControlRotation (accessed via GetControlRotation()), determines the aiming
 * orientation of the controlled Pawn.
 *
 * In networked games, PlayerControllers exist on the server for every player-controlled pawn,
 * and also on the controlling client's machine. They do NOT exist on a client's
 * machine for pawns controlled by remote players elsewhere on the network.
 *
 * @see https://docs.unrealengine.com/latest/INT/Gameplay/Framework/Controller/PlayerController/
 */
public class PlayerController : Controller
{
    public Player Player { get; set; }
    public bool IsInputEnable { get; set; }

    // ---- View assignment ----

    /// <summary>
    /// The <see cref="ViewId"/> of the <see cref="RenderView"/> assigned to this player.
    /// Set by <see cref="ViewManager.AssignPlayer"/> (or equivalent) before gameplay begins.
    /// <see cref="ViewId.Empty"/> means no view is assigned yet.
    /// </summary>
    public ViewId AssignedViewId { get; set; } = ViewId.Empty;

    // ---- UI integration ----

    /// <summary>
    /// The UI runtime for this player's assigned view.
    /// Set by the system when the player is assigned to a view.
    /// Null until the player has a view and its UI runtime has been created.
    /// </summary>
    public IUIViewRuntime? UIView { get; set; }

    /// <summary>
    /// Adds <paramref name="screen"/> to this player's HUD layer
    /// (<see cref="UILayer.HUD"/>) via the assigned UI runtime.
    /// No-op if no UI runtime is assigned.
    /// </summary>
    public void AddScreenToHUD(IUIScreen screen)
    {
        UIView?.PushScreen(screen);
    }

    /// <summary>Removes <paramref name="screen"/> from the HUD stack.</summary>
    public void RemoveScreenFromHUD(IUIScreen screen)
    {
        UIView?.RemoveScreen(screen);
    }

    /// <summary>
    /// Pushes a pause menu screen onto the <see cref="UILayer.Menu"/> layer.
    /// Override in derived classes to push a project-specific pause screen.
    /// </summary>
    public virtual void ShowPauseMenu()
    {
        // Derived classes should push a concrete IUIScreen with Layer == UILayer.Menu.
    }

    /// <summary>
    /// Pops the topmost menu screen, resuming gameplay.
    /// Override in derived classes to match the corresponding <see cref="ShowPauseMenu"/> logic.
    /// </summary>
    public virtual void HidePauseMenu()
    {
        // Derived classes should pop or remove the pause screen they pushed.
    }
}
