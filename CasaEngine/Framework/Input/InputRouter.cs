using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.GUI;
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
/// find the view under the cursor. The hosted UI runtime already handles first-chance
/// mouse input inside each view; gameplay
/// code can check <c>IsMouseHandledByUI(view)</c> to skip duplicate handling.
/// </summary>
public sealed class InputRouter
{
    private sealed class ViewInputRegistration
    {
        public required IWindowInputSource WindowInputSource { get; init; }
    }

    private readonly ViewManager _viewManager;
    private readonly Dictionary<PlayerIndex, ViewId> _playerViews = new();
    private readonly Dictionary<ViewId, ViewInputRegistration> _viewInputs = new();
    private readonly Dictionary<ViewId, MouseState> _previousMouseStatesByView = new();
    private MouseState? _previousFallbackMouseState;
    private ViewInputRegistration? _fallbackInput;

    public ViewId CurrentTargetViewId { get; private set; } = ViewId.Empty;
    public ViewId CurrentPointerViewId { get; private set; } = ViewId.Empty;
    public ViewId KeyboardFocusViewId { get; private set; } = ViewId.Empty;
    public ViewId ModalViewId { get; private set; } = ViewId.Empty;
    public ViewId UIPointerCaptureViewId { get; private set; } = ViewId.Empty;
    public InputRoutingState CurrentRoutingState { get; private set; } = InputRoutingState.Empty;
    public ViewInputContext CurrentInputContext { get; private set; } = ViewInputContext.Empty;

    public bool HasRegisteredViewInputSources => _viewInputs.Count > 0;
    public bool HasAnyRegisteredInputSources => _fallbackInput != null || _viewInputs.Count > 0;

    public InputRouter(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    public void RegisterViewInput(
        ViewId viewId,
        IWindowInputSource windowInputSource)
    {
        ArgumentNullException.ThrowIfNull(windowInputSource);

        _viewInputs[viewId] = new ViewInputRegistration
        {
            WindowInputSource = windowInputSource,
        };
    }

    public void RegisterFallbackInput(IWindowInputSource windowInputSource)
    {
        ArgumentNullException.ThrowIfNull(windowInputSource);

        _fallbackInput = new ViewInputRegistration
        {
            WindowInputSource = windowInputSource,
        };
    }

    public void ClearFallbackInput()
    {
        _fallbackInput = null;
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

        if (UIPointerCaptureViewId == viewId)
        {
            UIPointerCaptureViewId = ViewId.Empty;
        }

        _previousMouseStatesByView.Remove(viewId);
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
        if (TryDispatchContext(out var context))
        {
            viewId = context.ViewId;
            keyboardState = context.KeyboardState;
            mouseState = context.MouseState;
            return true;
        }

        viewId = ViewId.Empty;
        keyboardState = new KeyboardState();
        mouseState = new MouseState();
        return false;
    }

    public bool TryDispatchContext(out ViewInputContext context)
    {
        _viewManager.SynchronizeHostStates();

        var routingState = ResolveRoutingState();
        CurrentRoutingState = routingState;
        ModalViewId = routingState.ModalViewId;
        CurrentPointerViewId = routingState.PointerViewId;
        UIPointerCaptureViewId = routingState.UIPointerCaptureViewId;
        CurrentTargetViewId = routingState.TargetViewId;

        var targetView = routingState.TargetViewId.IsEmpty
            ? null
            : _viewManager.TryGetView(routingState.TargetViewId, out var resolvedView)
                ? resolvedView
                : null;
        if (targetView == null)
        {
            if (_fallbackInput != null)
            {
                CurrentRoutingState = routingState with { Reason = InputRoutingReason.Fallback };
                var snapshot = _fallbackInput.WindowInputSource.GetSnapshot();
                var keyboardState = snapshot.KeyboardState;
                var mouseState = snapshot.MouseState;
                context = CreateInputContext(ViewId.Empty, snapshot.FrameId, keyboardState, mouseState, Rectangle.Empty, CurrentRoutingState, isFallback: true);
                CurrentInputContext = context;
                return true;
            }

            CurrentRoutingState = InputRoutingState.Empty;
            CurrentInputContext = ViewInputContext.Empty;
            context = ViewInputContext.Empty;
            return false;
        }

        if (!_viewInputs.TryGetValue(targetView.Id, out var registration))
        {
            CurrentTargetViewId = ViewId.Empty;
            CurrentInputContext = ViewInputContext.Empty;
            context = ViewInputContext.Empty;
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

        var routedSnapshot = registration.WindowInputSource.GetSnapshot();
        var routedKeyboardState = routedSnapshot.KeyboardState;
        var screenMouseState = routedSnapshot.MouseState;
        var screenBounds = GetScreenBounds(targetView);
        var routedMouseState = CreateLocalMouseState(screenMouseState, screenBounds);
        context = CreateInputContext(targetView.Id, routedSnapshot.FrameId, routedKeyboardState, routedMouseState, screenBounds, routingState, isFallback: false);
        CurrentInputContext = context;
        return true;
    }

    private ViewInputContext CreateInputContext(
        ViewId viewId,
        long frameId,
        KeyboardState keyboardState,
        MouseState mouseState,
        Rectangle screenBounds,
        InputRoutingState routingState,
        bool isFallback)
    {
        var previousMouseState = isFallback
            ? _previousFallbackMouseState ?? mouseState
            : _previousMouseStatesByView.TryGetValue(viewId, out var previousState)
                ? previousState
                : mouseState;

        var localPosition = mouseState.Position;
        var screenPosition = viewId.IsEmpty
            ? localPosition
            : new Point(screenBounds.X + localPosition.X, screenBounds.Y + localPosition.Y);

        var context = new ViewInputContext(
            viewId,
            frameId,
            keyboardState,
            mouseState,
            screenBounds,
            screenPosition,
            localPosition,
            mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue,
            mouseState.HorizontalScrollWheelValue - previousMouseState.HorizontalScrollWheelValue,
            routingState);

        if (isFallback)
        {
            _previousFallbackMouseState = mouseState;
        }
        else
        {
            _previousMouseStatesByView[viewId] = mouseState;
        }

        return context;
    }

    private static Rectangle GetScreenBounds(RenderView view)
    {
        return view.Host is IViewScreenBoundsHost screenBoundsHost
            ? screenBoundsHost.ScreenBounds
            : view.Surface.ViewportRect;
    }

    private static MouseState CreateLocalMouseState(MouseState screenMouseState, Rectangle screenBounds)
    {
        return new MouseState(
            screenMouseState.X - screenBounds.X,
            screenMouseState.Y - screenBounds.Y,
            screenMouseState.ScrollWheelValue,
            screenMouseState.LeftButton,
            screenMouseState.MiddleButton,
            screenMouseState.RightButton,
            screenMouseState.XButton1,
            screenMouseState.XButton2,
            screenMouseState.HorizontalScrollWheelValue);
    }

    public ViewId GetCapturedViewId()
    {
        return _viewManager.InputCaptureView?.Id ?? ViewId.Empty;
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

    public IUIViewRuntime? GetUIViewForPlayer(PlayerIndex playerIndex)
    {
        return GetRenderViewForPlayer(playerIndex)?.UIView;
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
        return view.UIView?.InputState.IsPointerOverUI ?? false;
    }

    /// <summary>
    /// Returns true if a UI element in <paramref name="view"/> has keyboard focus
    /// (e.g. a text box that is actively receiving key input).
    /// Pass this result to gameplay systems to suppress hotkey handling.
    /// </summary>
    public bool IsKeyboardCapturedByUI(RenderView view)
    {
        return view.UIView?.InputState.IsKeyboardCaptured ?? false;
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

            if (view.UIView?.InputState.HasModalInput == true)
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

        if (_fallbackInput != null)
        {
            var pointerScreenPosition = _fallbackInput.WindowInputSource.GetSnapshot().MouseState.Position;
            var (view, _) = _viewManager.ScreenToView(pointerScreenPosition);
            if (view != null && _viewInputs.ContainsKey(view.Id))
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

    private RenderView? ResolveUiPointerCaptureView(RenderView? modalView)
    {
        if (modalView != null)
        {
            return modalView.UIView?.InputState.IsPointerCaptured == true ? modalView : null;
        }

        for (int i = _viewManager.Views.Count - 1; i >= 0; i--)
        {
            var view = _viewManager.Views[i];
            if (!view.Enabled || !view.IsVisible || !_viewInputs.ContainsKey(view.Id))
            {
                continue;
            }

            if (view.UIView?.InputState.IsPointerCaptured == true)
            {
                return view;
            }
        }

        return null;
    }

    private InputRoutingState ResolveRoutingState()
    {
        var modalView = ResolveModalView();
        var modalViewId = modalView?.Id ?? ViewId.Empty;
        var captureViewId = GetCapturedViewId();
        var uiPointerCaptureView = ResolveUiPointerCaptureView(modalView);
        var uiPointerCaptureViewId = uiPointerCaptureView?.Id ?? ViewId.Empty;
        var pointerView = ResolvePointerView(modalView);
        var pointerViewId = pointerView?.Id ?? ViewId.Empty;
        var keyboardFocusView = ResolveKeyboardFocusView();
        var keyboardFocusViewId = keyboardFocusView?.Id ?? KeyboardFocusViewId;

        if (modalView != null)
        {
            return new InputRoutingState(
                modalView.Id,
                pointerViewId,
                keyboardFocusViewId,
                modalViewId,
                captureViewId,
                uiPointerCaptureViewId,
                InputRoutingReason.Modal);
        }

        if (!captureViewId.IsEmpty)
        {
            return new InputRoutingState(
                captureViewId,
                pointerViewId,
                keyboardFocusViewId,
                modalViewId,
                captureViewId,
                uiPointerCaptureViewId,
                InputRoutingReason.Capture);
        }

            if (uiPointerCaptureView != null)
            {
                return new InputRoutingState(
                uiPointerCaptureView.Id,
                pointerViewId,
                keyboardFocusViewId,
                modalViewId,
                captureViewId,
                uiPointerCaptureViewId,
                InputRoutingReason.UIPointerCapture);
            }

        if (pointerView != null)
        {
            return new InputRoutingState(
                pointerView.Id,
                pointerViewId,
                keyboardFocusViewId,
                modalViewId,
                captureViewId,
                uiPointerCaptureViewId,
                InputRoutingReason.Pointer);
        }

        if (keyboardFocusView != null)
        {
            return new InputRoutingState(
                keyboardFocusView.Id,
                pointerViewId,
                keyboardFocusView.Id,
                modalViewId,
                captureViewId,
                uiPointerCaptureViewId,
                InputRoutingReason.KeyboardFocus);
        }

        return new InputRoutingState(
            ViewId.Empty,
            pointerViewId,
            keyboardFocusViewId,
            modalViewId,
            captureViewId,
            uiPointerCaptureViewId,
            _fallbackInput != null ? InputRoutingReason.Fallback : InputRoutingReason.None);
    }
}
