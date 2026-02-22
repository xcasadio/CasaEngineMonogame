using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.Input;

/// <summary>
/// Routes per-frame input to the correct <see cref="RenderView"/> and arbitrates
/// between UI (MGUI) and gameplay consumers.
///
/// <b>Player assignment:</b> in split-screen setups each <see cref="PlayerIndex"/>
/// is mapped to a <see cref="ViewId"/> so that gamepad and abstracted keyboard
/// input are sent to the correct view / <c>PlayerController</c>.
///
/// <b>Mouse routing:</b> delegates to <see cref="ViewManager.ScreenToView"/> to
/// find the view under the cursor. MGUI already handles first-chance mouse input
/// inside each <see cref="UIRoot"/> via <see cref="GUI.ViewRenderHost"/>; gameplay
/// code can check <c>IsMouseHandledByUI(view)</c> to skip duplicate handling.
/// </summary>
public sealed class InputRouter
{
    private readonly ViewManager _viewManager;
    private readonly Dictionary<PlayerIndex, ViewId> _playerViews = new();

    public InputRouter(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    // ---- Player → View assignment ----

    /// <summary>
    /// Assigns <paramref name="playerIndex"/> to the view identified by <paramref name="viewId"/>.
    /// The corresponding <see cref="PlayerController"/> will receive input intended for this player.
    /// </summary>
    public void AssignPlayer(PlayerIndex playerIndex, ViewId viewId)
    {
        _playerViews[playerIndex] = viewId;
    }

    /// <summary>Removes the player-to-view assignment for <paramref name="playerIndex"/>.</summary>
    public void UnassignPlayer(PlayerIndex playerIndex)
    {
        _playerViews.Remove(playerIndex);
    }

    /// <summary>
    /// Returns the <see cref="ViewId"/> assigned to <paramref name="playerIndex"/>,
    /// or <see cref="ViewId.Empty"/> if no assignment exists.
    /// </summary>
    public ViewId GetViewForPlayer(PlayerIndex playerIndex)
        => _playerViews.TryGetValue(playerIndex, out var id) ? id : ViewId.Empty;

    /// <summary>
    /// Returns the <see cref="RenderView"/> assigned to <paramref name="playerIndex"/>,
    /// or null if no assignment or the view could not be found.
    /// </summary>
    public RenderView? GetRenderViewForPlayer(PlayerIndex playerIndex)
    {
        if (_playerViews.TryGetValue(playerIndex, out var id)
            && _viewManager.TryGetView(id, out var view))
            return view;
        return null;
    }

    // ---- Mouse routing ----

    /// <summary>
    /// Finds which view the screen-space <paramref name="screenPoint"/> falls into
    /// and returns it together with the point expressed in view-local coordinates.
    /// Delegates to <see cref="ViewManager.ScreenToView"/>.
    /// </summary>
    public (RenderView? view, Vector2 localPoint) RouteMouseToView(Point screenPoint)
        => _viewManager.ScreenToView(screenPoint);

    /// <summary>
    /// Returns true if the MGUI desktop for <paramref name="view"/> currently has
    /// the mouse hovering over a UI element (i.e. a widget may handle click events).
    ///
    /// Returns true also when a UI element has keyboard focus (e.g. a text box),
    /// indicating that keyboard input should not be consumed by gameplay.
    ///
    /// Call this before processing gameplay mouse/keyboard input to avoid double-consuming events.
    /// </summary>
    public bool IsMouseHandledByUI(RenderView view)
    {
        var root = view.UIRoot;
        if (root == null) return false;

        // HoveredElement != null means the mouse is over a UI widget in that window.
        return root.Desktop.Windows.Any(w => w.HoveredElement != null);
    }

    /// <summary>
    /// Returns true if a UI element in <paramref name="view"/> has keyboard focus
    /// (e.g. a text box that is actively receiving key input).
    /// Pass this result to gameplay systems to suppress hotkey handling.
    /// </summary>
    public bool IsKeyboardCapturedByUI(RenderView view)
    {
        var root = view.UIRoot;
        return root?.Desktop.FocusedKeyboardHandler != null;
    }
}
