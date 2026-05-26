using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Runtime UI hosted by one render view.
/// Implementations may wrap MGUI, XAML, or another UI runtime.
/// </summary>
public interface IUIViewRuntime : IDisposable
{
    /// <summary>Updates the runtime for the current frame.</summary>
    void Update(GameTime gameTime);

    /// <summary>Draws the runtime into the currently active surface.</summary>
    void Draw();

    /// <summary>True when the pointer currently targets an interactive UI element.</summary>
    bool IsPointerOverUI { get; }

    /// <summary>True when the UI runtime is actively holding a pointer interaction across frames.</summary>
    bool IsPointerCaptured { get; }

    /// <summary>True when keyboard input is currently captured by the UI runtime.</summary>
    bool IsKeyboardCaptured { get; }

    /// <summary>Aggregated UI routing state consumed by the engine input router.</summary>
    UIViewInputState InputState { get; }

    /// <summary>
    /// True when the hosted UI is modal and should block lower-priority engine consumers.
    /// The engine input router gives this state priority over capture, pointer hover and keyboard focus.
    /// </summary>
    bool HasModalInput { get; }

    /// <summary>Latest per-view UI metrics computed by the host runtime.</summary>
    UIViewMetrics Metrics { get; }

    /// <summary>Refreshes per-view UI metrics such as scale and safe area.</summary>
    void UpdateMetrics(UIViewMetrics metrics);

    /// <summary>Pushes a screen into the hosted UI stack when supported.</summary>
    void PushScreen(IUIScreen screen);

    /// <summary>Pops the topmost screen from the hosted UI stack when supported.</summary>
    IUIScreen PopScreen();

    /// <summary>Removes the given screen from the hosted UI stack when supported.</summary>
    void RemoveScreen(IUIScreen screen);
}