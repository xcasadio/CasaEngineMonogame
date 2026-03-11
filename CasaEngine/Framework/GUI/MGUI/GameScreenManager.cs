using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Manages global game-state-driven screen transitions across all active views.
///
/// Listens to <see cref="GameFramework.GameMode.GameStateChanged"/> (or equivalent)
/// and instructs each view's hosted UI runtime to push/pop the
/// appropriate <see cref="IUIScreen"/> for the new state.
///
/// <b>Usage pattern:</b>
/// <code>
/// var gsm = new GameScreenManager(viewManager);
/// gsm.RegisterFactory("MainMenu", () => new MainMenuScreen());
/// gameMode.GameStateChanged += (_, newState) => gsm.TransitionTo(newState);
/// </code>
/// </summary>
public sealed class GameScreenManager
{
    private readonly ViewManager _viewManager;

    /// <summary>Maps game-state names to factory functions that create the screen.</summary>
    private readonly Dictionary<string, Func<IUIScreen>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The currently active state name, or null if none.</summary>
    public string? CurrentState { get; private set; }

    public GameScreenManager(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    /// <summary>
    /// Registers a factory for a named game state.
    /// When <see cref="TransitionTo"/> is called with <paramref name="stateName"/>,
    /// the factory is invoked once per view to create the screen.
    /// </summary>
    public void RegisterFactory(string stateName, Func<IUIScreen> factory)
    {
        _factories[stateName] = factory;
    }

    /// <summary>
    /// Pops the current state's screen (if any) from all view stacks and pushes
    /// the screen for <paramref name="newState"/>.
    /// No-op if <paramref name="newState"/> equals the current state.
    /// </summary>
    public void TransitionTo(string newState)
    {
        if (string.Equals(CurrentState, newState, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var view in _viewManager.Views)
        {
            var uiView = view.UIView;
            if (uiView == null) continue;

            // Pop existing state if there is one.
            if (CurrentState != null)
                uiView.PopScreen();

            // Push the new state if a factory exists for it.
            if (_factories.TryGetValue(newState, out var factory))
                uiView.PushScreen(factory());
        }

        CurrentState = newState;
    }

    /// <summary>Pops the current state screen from all view stacks.</summary>
    public void ClearState()
    {
        if (CurrentState == null) return;

        foreach (var view in _viewManager.Views)
            view.UIView?.PopScreen();

        CurrentState = null;
    }
}
