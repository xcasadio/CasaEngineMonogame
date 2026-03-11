using Microsoft.Xna.Framework;
using MGUI.Core.UI;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Manages an ordered stack of <see cref="IUIScreen"/> instances within a single
/// <see cref="UIRoot"/>. Screens are ordered bottom-to-top; the last element is
/// the topmost (drawn last, updated first).
///
/// <b>Modal-blocking rule:</b> when any screen has <see cref="IUIScreen.IsModal"/>
/// set to true, only that screen and any screens pushed on top of it receive
/// <see cref="IUIScreen.Update"/> calls. Lower screens are still drawn but frozen.
/// </summary>
public sealed class ScreenStack
{
    private readonly UIRoot         _root;
    private readonly List<IUIScreen> _screens = new();

    public ScreenStack(UIRoot root) => _root = root;

    /// <summary>Read-only ordered view of the stack (bottom = first, top = last).</summary>
    public IReadOnlyList<IUIScreen> Screens => _screens;

    /// <summary>The topmost screen, or null if the stack is empty.</summary>
    public IUIScreen? Top => _screens.Count > 0 ? _screens[^1] : null;

    /// <summary>True if any screen on the stack blocks lower-priority engine consumers.</summary>
    public bool HasModalInput => _screens.Exists(static s => s.BlocksViewsBelow);

    // ---- Push / Pop ----

    /// <summary>
    /// Pushes <paramref name="screen"/> onto the stack. If this is its first push,
    /// <see cref="IUIScreen.Initialize"/> is called before <see cref="IUIScreen.Show"/>.
    /// All windows returned by <see cref="IUIScreen.GetWindows"/> are registered with
    /// the desktop automatically.
    /// </summary>
    public void Push(IUIScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        // Initialize once.
        if (!_screens.Contains(screen))
        {
            screen.Initialize(_root);
            foreach (var w in screen.GetWindows())
                _root.Desktop.Windows.Add(w);
        }

        _screens.Add(screen);
        screen.Show();
    }

    /// <summary>
    /// Pops the topmost screen, calls <see cref="IUIScreen.Hide"/>, and removes
    /// its windows from the desktop.
    /// </summary>
    /// <returns>The popped screen, or null if the stack was empty.</returns>
    public IUIScreen? Pop()
    {
        if (_screens.Count == 0) return null;

        var screen = _screens[^1];
        _screens.RemoveAt(_screens.Count - 1);
        screen.Hide();

        foreach (var w in screen.GetWindows())
            _root.Desktop.Windows.Remove(w);

        return screen;
    }

    /// <summary>Removes a specific screen regardless of its stack position.</summary>
    public void Remove(IUIScreen screen)
    {
        if (!_screens.Remove(screen)) return;

        screen.Hide();
        foreach (var w in screen.GetWindows())
            _root.Desktop.Windows.Remove(w);
    }

    /// <summary>Pops every screen from the stack.</summary>
    public void Clear()
    {
        while (_screens.Count > 0)
            Pop();
    }

    // ---- Update ----

    /// <summary>
    /// Updates the stack according to the modal-blocking rule.
    /// If a modal screen exists, only screens at or above its index are updated.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        if (_screens.Count == 0) return;

        // Find the lowest blocking screen (blocks everything below it).
        int startIndex = 0;
        for (int i = _screens.Count - 1; i >= 0; i--)
        {
            if (_screens[i].BlocksViewsBelow)
            {
                startIndex = i;
                break;
            }
        }

        for (int i = startIndex; i < _screens.Count; i++)
            _screens[i].Update(gameTime);
    }
}
