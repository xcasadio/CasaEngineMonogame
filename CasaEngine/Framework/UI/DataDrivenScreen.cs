using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI;

/// <summary>
/// A screen with no code of its own: everything it does comes from its <c>.uiscreen</c> envelope, which is what a
/// project without a gameplay DLL has (ADR-0078). Its buttons reach the commands of the envelope through their
/// <c>CommandName</c>.
/// <para/>
/// <b>Invalid data fails here, at load:</b> the checks of <see cref="OnWindowLoaded"/> throw an
/// <see cref="InvalidOperationException"/> naming the envelope. The caller that built the screen disposes it then,
/// since it never reached a stack.
/// <para/>
/// <b>A command never runs inside MGUI's input handling,</b> because an action can change the screen stack: a click
/// or a Submit is remembered and <see cref="Update"/> runs it.
/// <para/>
/// <b>Nobody owns this screen once it is pushed,</b> so <see cref="Hide"/> disposes it: it cannot be pushed again.
/// <para/>
/// <b>When the project scales its screens,</b> the XAML is written in design pixels and the window follows the integer
/// factor of the view (ADR-0079).
/// <para/>
/// <b>Navigation comes from the envelope too</b> (ADR-0080): the focus can wrap at the ends, and a Cancel nothing
/// consumed and a focus move can each run a command.
/// <para/>
/// <b>A command can wait for a screen fade</b> (ADR-0081): <see cref="Update"/> then goes on with its list on a later
/// frame. Meanwhile no other command runs, and while the screen is at the top <see cref="UIRoot"/> stops the navigation
/// of the keyboard and the gamepad, which leaves a focus that exists in place.
/// </summary>
public sealed class DataDrivenScreen : XamlUIScreenBase
{
    private readonly UIScreenActionContext _context;

    // Held for the screen's lifetime, so that a sound plays without a load in the middle of a menu.
    private readonly Dictionary<string, AssetHandle<SoundAsset>> _sounds = new(StringComparer.Ordinal);

    // Held for the screen's lifetime: the family of a font must stay known to the UI while the screen shows it.
    private readonly List<IDisposable> _fonts = new();

    // What may take the focus, in order of preference. Emptied once the question is settled.
    private readonly List<MGElement> _focusCandidates = new();
    private UIScreenCommandData _pendingCommand;

    // The command whose list has begun and not ended, and the action it goes on with. Outside the call that runs it,
    // this is a list waiting for a fade.
    private UIScreenCommandData _runningCommand;
    private int _nextActionIndex;

    // True only inside the call that runs actions: a Hide outside it, a wait included, disposes at once.
    private bool _isRunningCommand;
    private bool _wasHiddenByItsCommand;

    // The entries of ui_commands the envelope names, null when it names none.
    private UIScreenCommandData _cancelCommand;
    private UIScreenCommandData _moveCommand;
    private UIScreenCommandData _onOpenCommand;

    private bool _isFocusWrapPending;

    // The pairs of focus_markers, resolved at load: element, then marker.
    private readonly List<(MGElement Element, MGElement Marker)> _focusMarkers = new();

    // The focus the markers last showed.
    private MGElement _markedFocus;

    // Where the focus was at the last Update, whichever window held it.
    private MGElement _lastFocused;

    // Set on the frame the default focus is asked for: MGUI applies it on the next one, and that change is no move.
    private bool _isDefaultFocusArriving;

    // Where the XAML puts the window, in design pixels. The window's own Left and Top hold the scaled position.
    private Point _designTopLeft;

    /// <summary>Builds the screen from a catalogued asset, held for the screen's lifetime.</summary>
    /// <exception cref="InvalidOperationException">No screen asset of that id or name can be resolved.</exception>
    public DataDrivenScreen(AssetContentManager assetContentManager, string assetIdOrName, UIScreenActionContext context)
        : base(assetContentManager, assetIdOrName)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Builds the screen from an envelope the caller read.</summary>
    /// <param name="asset">The envelope.</param>
    /// <param name="assetFilePath">Path of the file <paramref name="asset"/> was read from.</param>
    /// <param name="context">What the screen asks of the game.</param>
    public DataDrivenScreen(UIScreenAsset asset, string assetFilePath, UIScreenActionContext context)
        : base(asset, assetFilePath)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public override UILayer Layer => IsModal ? UILayer.Modal : UILayer.Menu;

    /// <inheritdoc/>
    public override bool IsModal => Asset.IsModal;

    /// <summary>
    /// True while a list of actions waits for a screen fade, frozen under a modal screen or not. See
    /// <see cref="ScreenStack.IsNavigationSuspended"/> (ADR-0081).
    /// </summary>
    public bool IsCommandSuspended => _runningCommand != null;

    /// <summary>
    /// Holds the fonts of the envelope, then loads the window. <see cref="OnWindowLoaded"/> would be too late: a text
    /// element resolves its family while the XAML loads, and one nobody holds falls back to the default font for good.
    /// </summary>
    /// <exception cref="InvalidOperationException">A font cannot be held.</exception>
    protected override MGWindow LoadWindow(MGDesktop desktop)
    {
        HoldFonts();
        return base.LoadWindow(desktop);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The envelope is invalid.</exception>
    protected override void OnWindowLoaded(MGWindow window)
    {
        if (Asset.CommandFormatErrors.Count > 0)
        {
            throw InvalidEnvelope(string.Join(" ", Asset.CommandFormatErrors));
        }

        RegisterCommands(window);
        ResolveNavigationCommands();
        CollectFocusCandidates(window);
        CheckButtons(window);
        ResolveFocusMarkers(window);
        PrepareDesignScale(window);
        _isFocusWrapPending = Asset.FocusWrap != UIScreenFocusWrap.None;
        StartOnOpenCommand();
    }

    /// <inheritdoc/>
    protected override void OnScreenBoundsChanged(Rectangle bounds) => ApplyDesignScale(bounds);

    /// <summary>
    /// Gives the default focus, sets the wrap up, and runs the command a click, a Submit, a Cancel or a focus move left,
    /// or goes on with the one waiting for a fade. A move is looked at first: the default focus it must not count is
    /// only asked for afterwards.
    /// </summary>
    public override void Update(GameTime gameTime)
    {
        KeepModalWindowInFront();
        WatchFocusMove();
        ApplyDefaultFocus();
        ApplyFocusWrap();
        ShowFocusMarkers();

        if (_pendingCommand != null)
        {
            _runningCommand = _pendingCommand;
            _nextActionIndex = 0;
            _pendingCommand = null;
        }

        if (_runningCommand != null)
        {
            RunCommand();
        }
    }

    /// <summary>
    /// Tells the screen a Cancel reached it with nothing having consumed it. It then runs the <c>cancel_command</c> of
    /// its envelope on its next <see cref="Update"/>, under the same rule as a click.
    /// </summary>
    public void ReportCancel()
    {
        if (_cancelCommand != null)
        {
            Remember(_cancelCommand);
        }
    }

    /// <summary>
    /// Disposes the screen, or leaves that to the end of the command when one of its actions closed it: the actions
    /// after the closing one still need the sounds.
    /// </summary>
    public override void Hide()
    {
        CloseWindow();

        if (_isRunningCommand)
        {
            _wasHiddenByItsCommand = true;
            return;
        }

        Dispose();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _runningCommand = null;

        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        _sounds.Clear();

        for (int i = 0; i < _fonts.Count; i++)
        {
            _fonts[i].Dispose();
        }

        _fonts.Clear();
        _focusMarkers.Clear();
        _markedFocus = null;
        _lastFocused = null;
        base.Dispose();
    }

    private void HoldFonts()
    {
        for (int i = 0; i < Asset.Fonts.Count; i++)
        {
            string font = Asset.Fonts[i];

            if (_context.AcquireFont == null)
            {
                throw InvalidEnvelope($"'fonts' names '{font}', but this host offers no fonts.");
            }

            try
            {
                _fonts.Add(_context.AcquireFont(font));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"UIScreen asset '{AssetFilePath}': font '{font}' cannot be loaded.", exception);
            }
        }
    }

    private void RegisterCommands(MGWindow window)
    {
        var resources = window.GetResources();

        foreach (var command in Asset.UICommands)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                throw InvalidEnvelope("A command in 'ui_commands' has no name.");
            }

            if (resources.Commands.ContainsKey(command.Name))
            {
                throw InvalidEnvelope($"Command '{command.Name}' is declared twice.");
            }

            foreach (var action in command.Actions)
            {
                PrepareAction(command, action);
            }

            CheckWaits(command);
            resources.AddCommand(command.Name, _ => Remember(command));
        }
    }

    private void ResolveNavigationCommands()
    {
        _cancelCommand = FindCommand("cancel_command", Asset.CancelCommand);
        _moveCommand = FindCommand("move_command", Asset.MoveCommand);
        _onOpenCommand = FindCommand("on_open_command", Asset.OnOpenCommand);

        if (_cancelCommand != null && !_context.HasUIInputMapping)
        {
            throw InvalidEnvelope("'cancel_command' needs a project with a UIInputMapping: nothing else reports a Cancel.");
        }

        CheckOnOpenCommand();
    }

    /// <summary>
    /// The screen is not on the stack yet when this command starts, so nothing here may change the stack or the world:
    /// what that would do has no meaning before the screen exists for the game.
    /// </summary>
    private void CheckOnOpenCommand()
    {
        if (_onOpenCommand == null)
        {
            return;
        }

        foreach (var action in _onOpenCommand.Actions)
        {
            if (action.Type is not (UIScreenActionType.PlaySound or UIScreenActionType.FadeScreen or UIScreenActionType.WaitFade))
            {
                throw InvalidEnvelope(
                    $"'on_open_command' names '{_onOpenCommand.Name}', which has a '{UIScreenActionTypeNames.ToName(action.Type)}' action: only 'play_sound', 'fade_screen' and 'wait_fade' can run when a screen opens.");
            }
        }
    }

    /// <summary>
    /// Runs here and not in <see cref="Update"/> because loading is the one place every way of opening a screen goes
    /// through, so the fade runs from the frame the screen opens. No frame order asks for this on most screens: a first
    /// screen gets its first update before its first draw, and a screen opened by <c>open_screen</c> gets it on the
    /// frame it opens, inside the loop of <see cref="ScreenStack.Update"/>. For a fade it changes one case, a screen
    /// that gets no update on the frame it opens: the overlay still draws on that frame, which has no window of this
    /// screen in any case, see the limits of ADR-0082. The caller disposes the screen if an action throws.
    /// </summary>
    private void StartOnOpenCommand()
    {
        if (_onOpenCommand != null)
        {
            _runningCommand = _onOpenCommand;
            _nextActionIndex = 0;
            RunCommand();
        }
    }

    /// <summary>The command a field names, or null when it names none.</summary>
    private UIScreenCommandData FindCommand(string field, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var command in Asset.UICommands)
        {
            if (command.Name == name)
            {
                return command;
            }
        }

        throw InvalidEnvelope($"'{field}' names command '{name}', which 'ui_commands' does not declare.");
    }

    /// <summary>Checks one action, and takes the hold a sound needs.</summary>
    private void PrepareAction(UIScreenCommandData command, UIScreenActionData action)
    {
        switch (action.Type)
        {
            case UIScreenActionType.OpenScreen:
                RequireTarget(command, action);
                if (!_context.ScreenExists(action.Target))
                {
                    throw InvalidEnvelope($"Command '{command.Name}' opens screen '{action.Target}', which does not exist.");
                }

                break;

            case UIScreenActionType.ChangeWorld:
                RequireTarget(command, action);
                if (!_context.WorldExists(action.Target))
                {
                    throw InvalidEnvelope($"Command '{command.Name}' loads world '{action.Target}', which is not catalogued.");
                }

                break;

            case UIScreenActionType.PlaySound:
                RequireTarget(command, action);
                HoldSound(command, action.Target);
                break;

            case UIScreenActionType.CloseScreen:
            case UIScreenActionType.Quit:
            case UIScreenActionType.WaitFade:
                RefuseTarget(command, action);
                break;

            case UIScreenActionType.FadeScreen:
                RefuseTarget(command, action);
                CheckFade(command, action);
                break;

            default:
                throw InvalidEnvelope(
                    $"Command '{command.Name}' has an action of unknown type: {action.UnknownData?.ToString(Newtonsoft.Json.Formatting.None)}.");
        }
    }

    private void RequireTarget(UIScreenCommandData command, UIScreenActionData action)
    {
        if (string.IsNullOrWhiteSpace(action.Target))
        {
            throw InvalidEnvelope(
                $"Command '{command.Name}' has a '{UIScreenActionTypeNames.ToName(action.Type)}' action with no target.");
        }
    }

    private void RefuseTarget(UIScreenCommandData command, UIScreenActionData action)
    {
        if (!string.IsNullOrWhiteSpace(action.Target))
        {
            throw InvalidEnvelope(
                $"Command '{command.Name}' gives the target '{action.Target}' to '{UIScreenActionTypeNames.ToName(action.Type)}', which takes none.");
        }
    }

    /// <summary>A fade has no default: a field the file left out is an error, and the load reported any other.</summary>
    private void CheckFade(UIScreenCommandData command, UIScreenActionData action)
    {
        RequireFadeField(command, action.Layer.HasValue, "layer");
        RequireFadeField(command, action.BlendMode.HasValue, "blend_mode");
        RequireFadeField(command, action.From.HasValue, "from");
        RequireFadeField(command, action.To.HasValue, "to");
        RequireFadeField(command, action.DurationSeconds.HasValue, "duration_seconds");

        float duration = action.DurationSeconds.Value;
        if (!float.IsFinite(duration) || duration < 0f)
        {
            throw InvalidEnvelope($"Command '{command.Name}' has a 'fade_screen' action with a 'duration_seconds' of {duration}: it must be zero or more.");
        }
    }

    private void RequireFadeField(UIScreenCommandData command, bool hasValue, string field)
    {
        if (!hasValue)
        {
            throw InvalidEnvelope($"Command '{command.Name}' has a 'fade_screen' action with no '{field}'.");
        }
    }

    /// <summary>
    /// Only what surely leaves the screen running may come before a wait: a modal screen opened by the list freezes
    /// this one, and a list that closed it, changed the world or quit has nobody left to resume it. It would die with
    /// no sign. A non-modal screen would not stop it and is refused all the same: this reads the type of the action,
    /// not the screen it opens.
    /// </summary>
    private void CheckWaits(UIScreenCommandData command)
    {
        UIScreenActionData blocker = null;

        foreach (var action in command.Actions)
        {
            if (action.Type == UIScreenActionType.WaitFade)
            {
                if (blocker != null)
                {
                    throw InvalidEnvelope(
                        $"Command '{command.Name}' has a 'wait_fade' after '{UIScreenActionTypeNames.ToName(blocker.Type)}': only 'play_sound' and 'fade_screen' can come before a wait.");
                }
            }
            else if (action.Type is not (UIScreenActionType.PlaySound or UIScreenActionType.FadeScreen))
            {
                blocker ??= action;
            }
        }
    }

    private void HoldSound(UIScreenCommandData command, string target)
    {
        if (_sounds.ContainsKey(target))
        {
            return;
        }

        try
        {
            _sounds.Add(target, _context.AcquireSound(target));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"UIScreen asset '{AssetFilePath}': command '{command.Name}' plays sound '{target}', which cannot be loaded.",
                exception);
        }
    }

    /// <summary>
    /// Without <c>default_focus</c> the candidates are the buttons, in order: which of them can take the focus is only
    /// known once the layout has run. Components are skipped because the window's close button is one.
    /// </summary>
    private void CollectFocusCandidates(MGWindow window)
    {
        if (string.IsNullOrWhiteSpace(Asset.DefaultFocus))
        {
            _focusCandidates.AddRange(window.TraverseVisualTree<MGButton>(
                includeComponents: false, includeToolTips: false, includeContextMenus: false));
            return;
        }

        if (!window.TryGetElementByName(Asset.DefaultFocus, out MGElement element))
        {
            throw InvalidEnvelope($"'default_focus' names '{Asset.DefaultFocus}', which the XAML does not declare.");
        }

        if (!element.CanHandleKeyboardInput)
        {
            throw InvalidEnvelope($"'default_focus' names '{Asset.DefaultFocus}', a {element.GetType().Name} that cannot take the focus.");
        }

        _focusCandidates.Add(element);
    }

    /// <summary>
    /// A button that names no command is legal. One that names a command nobody declared would be a button that does
    /// nothing, with no sign why. The lookup goes through the resources so that a command the host registered counts.
    /// </summary>
    private void CheckButtons(MGWindow window)
    {
        foreach (var button in window.TraverseVisualTree<MGButton>())
        {
            if (!string.IsNullOrEmpty(button.CommandName) && !button.GetResources().TryGetCommand(button.CommandName, out _))
            {
                throw InvalidEnvelope($"A button names command '{button.CommandName}', which 'ui_commands' does not declare.");
            }
        }
    }

    /// <summary>
    /// Every marker starts hidden: no focus has arrived yet, and Hidden keeps its place in the layout. The pairs are
    /// checked against each other once all of them are resolved.
    /// </summary>
    private void ResolveFocusMarkers(MGWindow window)
    {
        foreach (var pair in Asset.FocusMarkers)
        {
            if (!window.TryGetElementByName(pair.Element, out MGElement element))
            {
                throw InvalidEnvelope($"'focus_markers' names element '{pair.Element}', which the XAML does not declare.");
            }

            if (!element.CanHandleKeyboardInput)
            {
                throw InvalidEnvelope($"'focus_markers' names element '{pair.Element}', a {element.GetType().Name} that cannot take the focus.");
            }

            if (!window.TryGetElementByName(pair.Marker, out MGElement marker))
            {
                throw InvalidEnvelope($"'focus_markers' names marker '{pair.Marker}', which the XAML does not declare.");
            }

            _focusMarkers.Add((element, marker));
        }

        for (int i = 0; i < _focusMarkers.Count; i++)
        {
            CheckFocusMarker(i);
            _focusMarkers[i].Marker.Visibility = Visibility.Hidden;
        }
    }

    /// <summary>
    /// Refuses the two shapes that would make a marker lie with no error. A marker named twice: <see cref="ShowFocusMarkers"/>
    /// writes it pair by pair, so only the last pair would drive it. A marker that is, or holds, the element of any pair:
    /// hiding it would hide that element too, and would take its keyboard input (ADR-0082).
    /// </summary>
    private void CheckFocusMarker(int index)
    {
        var marker = _focusMarkers[index].Marker;
        string markerName = Asset.FocusMarkers[index].Marker;

        for (int i = 0; i < _focusMarkers.Count; i++)
        {
            if (i != index && _focusMarkers[i].Marker == marker)
            {
                throw InvalidEnvelope(
                    $"'focus_markers' names marker '{markerName}' in two pairs, for elements '{Asset.FocusMarkers[index].Element}' and '{Asset.FocusMarkers[i].Element}': only the last pair would drive it.");
            }

            if (marker.IsSelfOrAncestorOf(_focusMarkers[i].Element))
            {
                throw InvalidEnvelope(
                    $"'focus_markers' names marker '{markerName}', which is element '{Asset.FocusMarkers[i].Element}' or contains it: hiding the marker would hide that element too.");
            }
        }
    }

    /// <summary>
    /// Writes only when the focus changed: this runs every frame, and a marker is not expected to change otherwise.
    /// This reads the focus rather than listening to <see cref="MGDesktop.FocusedKeyboardHandlerChanged"/>, which also
    /// fires for a screen frozen under a modal one: that screen gets no <see cref="Update"/>, so it keeps its markers.
    /// </summary>
    private void ShowFocusMarkers()
    {
        var focused = Window.Desktop.FocusedKeyboardHandler;

        if (focused == _markedFocus)
        {
            return;
        }

        _markedFocus = focused;

        for (int i = 0; i < _focusMarkers.Count; i++)
        {
            var (element, marker) = _focusMarkers[i];
            marker.Visibility = element == focused ? Visibility.Visible : Visibility.Hidden;
        }
    }

    /// <summary>
    /// A window placed by alignment is refused: <see cref="MGWindow.ApplyScreenPlacement"/> computes its position from
    /// its size in screen pixels, which ignores the scale.
    /// </summary>
    private void PrepareDesignScale(MGWindow window)
    {
        if (_context.DesignResolution == null)
        {
            return;
        }

        if (window.HasScreenPlacement)
        {
            throw InvalidEnvelope("A window placed by alignment cannot follow the design resolution: declare its Left and Top.");
        }

        _designTopLeft = window.TopLeft;
        ApplyDesignScale(window.Desktop.ValidScreenBounds);
    }

    /// <summary>
    /// <see cref="MGWindow.Scale"/> scales from the window's top-left corner and leaves that corner in screen pixels,
    /// so the position is scaled here. It is written first, because <see cref="MGWindow.Scale"/> builds its transforms
    /// from it. Writing it does not move the elements, and the bounds change after the desktop's update: without the
    /// validation, the next frame would draw them under the new transform at their old place.
    /// </summary>
    private void ApplyDesignScale(Rectangle bounds)
    {
        if (_context.DesignResolution is not { } design)
        {
            return;
        }

        int scale = VirtualResolutionLayout.Compute(bounds.Width, bounds.Height, design.X, design.Y).Scale;
        Window.Left = _designTopLeft.X * scale;
        Window.Top = _designTopLeft.Y * scale;
        Window.Scale = scale;
        Window.ValidateWindowSizeAndPosition();
    }

    /// <summary>
    /// <see cref="ScreenStack"/> takes the window off the desktop without telling MGUI it closed, so the focus would
    /// stay on an element nothing updates, which leaves the keyboard dead for the screen below, and the desktop's focus
    /// history would keep the window alive. Closing it through MGUI settles both. <c>CanCloseWindow</c> only speaks for
    /// the user's close button, not for the screen.
    /// <para/>
    /// A window with an exit effect, its own or its theme's, gives the focus away while it is still on the desktop, and
    /// could give it to one of its own elements, which no key press would take back since <see cref="ScreenStack"/>
    /// removes the window right after this. Disabled, none of them is a candidate. The effect does not show.
    /// </summary>
    private void CloseWindow()
    {
        if (Window != null)
        {
            Window.CanCloseWindow = true;
            Window.IsEnabled = false;
            Window.TryCloseWindow();
        }
    }

    /// <summary>
    /// MGUI raises a window that is clicked, so a click on a frozen screen would bury the modal one under a window that
    /// drops its clicks, and nothing could close it any more. Runs before the frame is drawn, so nothing shows.
    /// </summary>
    private void KeepModalWindowInFront()
    {
        if (IsModal && _context.IsTopScreen(this))
        {
            Window.Desktop.BringToFront(Window);
        }
    }

    /// <summary>
    /// Asking for the focus before the target can receive keyboard input loses the request, and the frame that
    /// becomes true is not known in advance (<c>XamlCommandFocusTests</c>), hence the polling. It stops at the first
    /// candidate able to take the focus.
    /// </summary>
    private void ApplyDefaultFocus()
    {
        for (int i = 0; i < _focusCandidates.Count; i++)
        {
            var candidate = _focusCandidates[i];
            if (((IKeyboardHandlerHost)candidate).CanReceiveKeyboardInput())
            {
                if (!string.IsNullOrWhiteSpace(Asset.DefaultFocus) || IsFocusHeldByAnotherWindow())
                {
                    candidate.Focus(KeyboardFocusSource.Programmatic);
                    _isDefaultFocusArriving = true;
                }

                _focusCandidates.Clear();
                return;
            }
        }
    }

    /// <summary>
    /// Waits for the focus, because the list of <see cref="MGDesktop.GetFocusableElements"/> starts from the window that
    /// holds it. Runs once: an element shown or enabled later is not in the list. MGUI tries a neighbour before its
    /// geometry, and goes back to its geometry when the neighbour can no longer take the focus.
    /// </summary>
    private void ApplyFocusWrap()
    {
        if (!_isFocusWrapPending || Window.Desktop.FocusedKeyboardHandler?.SelfOrParentWindow != Window)
        {
            return;
        }

        _isFocusWrapPending = false;

        var elements = Window.Desktop.GetFocusableElements();
        if (elements.Count < 2)
        {
            return;
        }

        bool isVertical = Asset.FocusWrap == UIScreenFocusWrap.Vertical;
        var first = elements[0];
        var last = elements[elements.Count - 1];
        first.NavigationNeighbors[isVertical ? NavigationDirection.Up : NavigationDirection.Left] = last;
        last.NavigationNeighbors[isVertical ? NavigationDirection.Down : NavigationDirection.Right] = first;
    }

    /// <summary>
    /// Compares the focus with the last frame's rather than subscribing to it: this sees a move on any input path, and
    /// MGUI keeps the source of a focus change to itself. A focus that comes from nothing or from another window is an
    /// arrival, not a move, and the mouse does not count.
    /// </summary>
    private void WatchFocusMove()
    {
        var focused = Window.Desktop.FocusedKeyboardHandler;
        var previous = _lastFocused;
        bool isDefaultFocus = _isDefaultFocusArriving;

        _lastFocused = focused;
        _isDefaultFocusArriving = false;

        if (_moveCommand != null
            && !isDefaultFocus
            && focused != previous
            && previous?.SelfOrParentWindow == Window
            && focused?.SelfOrParentWindow == Window
            && Window.Desktop.ActiveInputMode != UIInputMode.Pointer)
        {
            Remember(_moveCommand);
        }
    }

    /// <summary>
    /// Stands in for <see cref="WatchFocusMove"/> on a frame a modal screen freezes this one. MGUI's navigation can
    /// still move the focus here, when that screen has nothing to focus or after a click on this one brought the focus
    /// here, and the first <see cref="Update"/> after it would take that for a move of its own frame. A move nothing
    /// watched runs no <c>move_command</c>.
    /// </summary>
    internal void FollowFocusWhileFrozen()
    {
        _lastFocused = Window.Desktop.FocusedKeyboardHandler;
        _isDefaultFocusArriving = false;
    }

    /// <summary>
    /// A screen that names no focus only takes it from another screen: MGUI gives none to a window opened while another
    /// one holds it, keyboard navigation stays inside the focused window, and a command below the top is dropped, so
    /// this screen could only be reached with the mouse. Otherwise the focus is left to MGUI.
    /// </summary>
    private bool IsFocusHeldByAnotherWindow()
        => Window.Desktop.FocusedKeyboardHandler is { } focused && focused.SelfOrParentWindow != Window;

    /// <summary>
    /// A screen frozen under a modal one receives no <see cref="Update"/>: a click kept for it would run when the
    /// modal one closes, so a click off the top of the stack is dropped. So is anything that arrives while a list
    /// waits for a fade, which also settles a second confirm during that fade.
    /// </summary>
    private void Remember(UIScreenCommandData command)
    {
        if (_pendingCommand == null && _runningCommand == null && _context.IsTopScreen(this))
        {
            _pendingCommand = command;
        }
    }

    /// <summary>
    /// Runs the running command from where it stopped. An action that throws ends the list: it is not resumed on the
    /// next frame.
    /// </summary>
    private void RunCommand()
    {
        bool isWaiting = false;
        _isRunningCommand = true;

        try
        {
            isWaiting = RunUntilWait();
        }
        finally
        {
            _isRunningCommand = false;

            if (!isWaiting)
            {
                _runningCommand = null;
            }

            if (_wasHiddenByItsCommand)
            {
                Dispose();
            }
        }
    }

    /// <returns>True when the list stopped on a fade that is still running.</returns>
    private bool RunUntilWait()
    {
        var actions = _runningCommand.Actions;

        while (_nextActionIndex < actions.Count)
        {
            var action = actions[_nextActionIndex];

            if (action.Type == UIScreenActionType.WaitFade && _context.IsFadeRunning())
            {
                return true;
            }

            _nextActionIndex++;
            Execute(action);
        }

        return false;
    }

    private void Execute(UIScreenActionData action)
    {
        switch (action.Type)
        {
            case UIScreenActionType.OpenScreen:
                _context.OpenScreen(action.Target);
                break;

            case UIScreenActionType.CloseScreen:
                _context.CloseScreen(this);
                break;

            case UIScreenActionType.ChangeWorld:
                _context.ChangeWorld(action.Target);
                break;

            case UIScreenActionType.PlaySound:
                _context.PlaySound(_sounds[action.Target].Asset);
                break;

            case UIScreenActionType.Quit:
                _context.Quit();
                break;

            case UIScreenActionType.FadeScreen:
                _context.StartFade(action);
                break;

            // WaitFade does nothing here: reaching this line means no fade runs.
        }
    }

    private InvalidOperationException InvalidEnvelope(string problem)
        => new($"UIScreen asset '{AssetFilePath}': {problem}");
}
