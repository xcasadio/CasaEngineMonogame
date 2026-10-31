using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
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
/// </summary>
public sealed class DataDrivenScreen : XamlUIScreenBase
{
    private readonly UIScreenActionContext _context;

    // Held for the screen's lifetime, so that a sound plays without a load in the middle of a menu.
    private readonly Dictionary<string, AssetHandle<SoundAsset>> _sounds = new(StringComparer.Ordinal);

    // What may take the focus, in order of preference. Emptied once the question is settled.
    private readonly List<MGElement> _focusCandidates = new();
    private UIScreenCommandData _pendingCommand;
    private bool _isRunningCommand;
    private bool _wasHiddenByItsCommand;

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

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The envelope is invalid.</exception>
    protected override void OnWindowLoaded(MGWindow window)
    {
        if (Asset.CommandFormatErrors.Count > 0)
        {
            throw InvalidEnvelope(string.Join(" ", Asset.CommandFormatErrors));
        }

        RegisterCommands(window);
        CollectFocusCandidates(window);
        CheckButtons(window);
    }

    /// <summary>Gives the default focus, and runs the command a click or a Submit left.</summary>
    public override void Update(GameTime gameTime)
    {
        KeepModalWindowInFront();
        ApplyDefaultFocus();

        if (_pendingCommand != null)
        {
            var command = _pendingCommand;
            _pendingCommand = null;
            Run(command);
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
        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        _sounds.Clear();
        base.Dispose();
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

            resources.AddCommand(command.Name, _ => Remember(command));
        }
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
                if (!string.IsNullOrWhiteSpace(action.Target))
                {
                    throw InvalidEnvelope(
                        $"Command '{command.Name}' gives the target '{action.Target}' to '{UIScreenActionTypeNames.ToName(action.Type)}', which takes none.");
                }

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
                }

                _focusCandidates.Clear();
                return;
            }
        }
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
    /// modal one closes, so a click off the top of the stack is dropped.
    /// </summary>
    private void Remember(UIScreenCommandData command)
    {
        if (_pendingCommand == null && _context.IsTopScreen(this))
        {
            _pendingCommand = command;
        }
    }

    private void Run(UIScreenCommandData command)
    {
        _isRunningCommand = true;

        try
        {
            for (int i = 0; i < command.Actions.Count; i++)
            {
                Execute(command.Actions[i]);
            }
        }
        finally
        {
            _isRunningCommand = false;

            if (_wasHiddenByItsCommand)
            {
                Dispose();
            }
        }
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
        }
    }

    private InvalidOperationException InvalidEnvelope(string problem)
        => new($"UIScreen asset '{AssetFilePath}': {problem}");
}
