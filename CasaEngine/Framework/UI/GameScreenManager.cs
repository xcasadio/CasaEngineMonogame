using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.UI;

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
    public string CurrentState { get; private set; }

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

    public void PushScreen(IUIScreen screen, ViewId viewId)
    {
        ArgumentNullException.ThrowIfNull(screen);
        _viewManager.GetUIView(viewId)?.PushScreen(screen);
    }

    public void PushScreenToActiveView(IUIScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var activeViewId = _viewManager.ActiveView?.Id ?? ViewId.Empty;
        if (activeViewId.IsEmpty)
        {
            return;
        }

        PushScreen(screen, activeViewId);
    }

    public void RemoveScreen(IUIScreen screen, ViewId viewId)
    {
        ArgumentNullException.ThrowIfNull(screen);
        _viewManager.GetUIView(viewId)?.RemoveScreen(screen);
    }

    public void RemoveScreenFromActiveView(IUIScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var activeViewId = _viewManager.ActiveView?.Id ?? ViewId.Empty;
        if (activeViewId.IsEmpty)
        {
            return;
        }

        RemoveScreen(screen, activeViewId);
    }

    /// <summary>
    /// Pops the current state's screen (if any) from all view stacks and pushes
    /// the screen for <paramref name="newState"/>.
    /// No-op if <paramref name="newState"/> equals the current state.
    /// </summary>
    public void TransitionTo(string newState)
    {
        TransitionTo(newState, null);
    }

    public void TransitionTo(string newState, IEnumerable<ViewId> targetViews)
    {
        if (string.Equals(CurrentState, newState, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var view in ResolveViews(targetViews))
        {
            var uiView = view.UIView;
            if (uiView == null)
            {
                continue;
            }

            // Pop existing state if there is one.
            if (CurrentState != null)
            {
                uiView.PopScreen();
            }

            // Push the new state if a factory exists for it.
            if (_factories.TryGetValue(newState, out var factory))
            {
                uiView.PushScreen(factory());
            }
        }

        CurrentState = newState;
    }

    /// <summary>Pops the current state screen from all view stacks.</summary>
    public void ClearState()
    {
        ClearState(null);
    }

    public void ClearState(IEnumerable<ViewId> targetViews)
    {
        if (CurrentState == null)
        {
            return;
        }

        foreach (var view in ResolveViews(targetViews))
            view.UIView?.PopScreen();

        CurrentState = null;
    }

    private IEnumerable<RenderView> ResolveViews(IEnumerable<ViewId> targetViews)
    {
        if (targetViews == null)
        {
            return _viewManager.Views;
        }

        var resolvedViews = new List<RenderView>();
        foreach (var viewId in targetViews)
        {
            if (_viewManager.TryGetView(viewId, out var view))
            {
                resolvedViews.Add(view);
            }
        }

        return resolvedViews;
    }
}
