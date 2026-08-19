using CasaEngine.Framework.UI;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Gameplay;

/// <summary>
/// The local player session: possesses an entity (imposing <see cref="CharacterControlMode.Player"/>
/// on it), and carries the <see cref="Player"/>/<see cref="PlayerIndex"/> identity, the per-player
/// input facade (<see cref="Input"/>), the assigned view, and UI integration.
/// </summary>
public class PlayerController : Controller
{
    protected override CharacterControlMode? PossessedControlMode => CharacterControlMode.Player;

    public Player Player { get; set; }
    public bool IsInputEnable { get; set; } = true;

    /// <summary>
    /// The <see cref="PlayerIndex"/> this controller acts for, resolved from
    /// <see cref="Player"/> when it is a <see cref="LocalPlayer"/>, otherwise
    /// <see cref="PlayerIndex.One"/>.
    /// </summary>
    public PlayerIndex PlayerIndex => Player is LocalPlayer localPlayer ? localPlayer.ControllerId : PlayerIndex.One;

    /// <summary>
    /// Per-player input facade, filtered by input-enable state, per-view routing and
    /// UI-first arbitration. Wired by the engine when the controller is created; may be
    /// null in headless or editor-preview contexts.
    /// </summary>
    public PlayerInput Input { get; internal set; }

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
    public IUIViewRuntime UIView { get; set; }

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
