using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Engine.Input.InputDeviceStateProviders;

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
/// find the view under the cursor. The hosted UI runtime already handles first-chance
/// mouse input inside each view; gameplay
/// code can check <c>IsMouseHandledByUI(view)</c> to skip duplicate handling.
/// </summary>
public sealed class InputRouter
{
    private sealed class ViewInputRegistration
    {
        public required IKeyboardStateProvider KeyboardProvider { get; init; }
        public required IMouseStateProvider MouseProvider { get; init; }
        public required Func<bool> IsPointerOver { get; init; }
    }

    private readonly ViewManager _viewManager;
    private readonly Dictionary<PlayerIndex, ViewId> _playerViews = new();
    private readonly Dictionary<ViewId, ViewInputRegistration> _viewInputs = new();

    public ViewId CurrentTargetViewId { get; private set; } = ViewId.Empty;
    public ViewId CurrentPointerViewId { get; private set; } = ViewId.Empty;
    public ViewId KeyboardFocusViewId { get; private set; } = ViewId.Empty;
    public ViewId ModalViewId { get; private set; } = ViewId.Empty;

    public bool HasRegisteredViewInputSources => _viewInputs.Count > 0;

    public InputRouter(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    public void RegisterViewInput(
        ViewId viewId,
        IKeyboardStateProvider keyboardProvider,
        IMouseStateProvider mouseProvider,
        Func<bool> isPointerOver)
    {
        ArgumentNullException.ThrowIfNull(keyboardProvider);
        ArgumentNullException.ThrowIfNull(mouseProvider);
        ArgumentNullException.ThrowIfNull(isPointerOver);

        _viewInputs[viewId] = new ViewInputRegistration
        {
            KeyboardProvider = keyboardProvider,
            MouseProvider = mouseProvider,
            IsPointerOver = isPointerOver,
        };
    }

    public void UnregisterViewInput(ViewId viewId)
    {
        _viewInputs.Remove(viewId);

        if (CurrentTargetViewId == viewId)
        {
            CurrentTargetViewId = ViewId.Empty;
        }

        if (CurrentPointerViewId == viewId)
        {
            CurrentPointerViewId = ViewId.Empty;
        }

        if (KeyboardFocusViewId == viewId)
        {
            KeyboardFocusViewId = ViewId.Empty;
        }

        if (ModalViewId == viewId)
        {
            ModalViewId = ViewId.Empty;
        }
    }

    public void SetKeyboardFocus(ViewId viewId)
    {
        KeyboardFocusViewId = viewId;
    }

    public void ClearKeyboardFocus(ViewId viewId)
    {
        if (KeyboardFocusViewId == viewId)
        {
            KeyboardFocusViewId = ViewId.Empty;
        }
    }

    public bool TryDispatch(out ViewId viewId, out KeyboardState keyboardState, out MouseState mouseState)
    {
        var modalView = ResolveModalView();
        ModalViewId = modalView?.Id ?? ViewId.Empty;

        var pointerView = ResolvePointerView(modalView);
        CurrentPointerViewId = pointerView?.Id ?? ViewId.Empty;

        var targetView = modalView ?? pointerView ?? ResolveKeyboardFocusView();
        if (targetView == null || !_viewInputs.TryGetValue(targetView.Id, out var registration))
        {
            CurrentTargetViewId = ViewId.Empty;
            viewId = ViewId.Empty;
            keyboardState = new KeyboardState();
            mouseState = new MouseState();
            return false;
        }

        if (_viewManager.ActiveView != targetView)
        {
            _viewManager.SetActive(targetView);
        }

        if (!targetView.Id.IsEmpty)
        {
            KeyboardFocusViewId = targetView.Id;
        }

        CurrentTargetViewId = targetView.Id;
        viewId = targetView.Id;
        keyboardState = registration.KeyboardProvider.GetState();
        mouseState = registration.MouseProvider.GetState();
        return true;
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

    public bool IsViewReceivingInput(ViewId viewId)
    {
        return !CurrentTargetViewId.IsEmpty && CurrentTargetViewId == viewId;
    }

    public bool IsKeyboardFocused(ViewId viewId)
    {
        return !KeyboardFocusViewId.IsEmpty && KeyboardFocusViewId == viewId;
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
        return view.UIView?.IsPointerOverUI ?? false;
    }

    /// <summary>
    /// Returns true if a UI element in <paramref name="view"/> has keyboard focus
    /// (e.g. a text box that is actively receiving key input).
    /// Pass this result to gameplay systems to suppress hotkey handling.
    /// </summary>
    public bool IsKeyboardCapturedByUI(RenderView view)
    {
        return view.UIView?.IsKeyboardCaptured ?? false;
    }

    private RenderView? ResolveModalView()
    {
        for (int i = _viewManager.Views.Count - 1; i >= 0; i--)
        {
            var view = _viewManager.Views[i];
            if (!view.Enabled || !view.IsVisible)
            {
                continue;
            }

            if (view.UIView?.HasModalInput == true)
            {
                return view;
            }
        }

        return null;
    }

    private RenderView? ResolvePointerView(RenderView? modalView)
    {
        if (modalView != null)
        {
            return modalView;
        }

        if (_viewManager.InputCaptureView != null
            && _viewInputs.ContainsKey(_viewManager.InputCaptureView.Id))
        {
            return _viewManager.InputCaptureView;
        }

        for (int i = _viewManager.Views.Count - 1; i >= 0; i--)
        {
            var view = _viewManager.Views[i];
            if (!view.Enabled || !view.IsVisible)
            {
                continue;
            }

            if (_viewInputs.TryGetValue(view.Id, out var registration)
                && registration.IsPointerOver())
            {
                return view;
            }
        }

        return null;
    }

    private RenderView? ResolveKeyboardFocusView()
    {
        if (KeyboardFocusViewId.IsEmpty)
        {
            return null;
        }

        if (_viewManager.TryGetView(KeyboardFocusViewId, out var view)
            && view.Enabled
            && view.IsVisible
            && _viewInputs.ContainsKey(view.Id))
        {
            return view;
        }

        KeyboardFocusViewId = ViewId.Empty;
        return null;
    }
}
