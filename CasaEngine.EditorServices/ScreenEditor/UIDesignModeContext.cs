namespace CasaEngine.EditorServices.ScreenEditor;

/// <summary>
/// Provides a global flag to distinguish design-time (editor preview) execution
/// from normal game-time execution.
/// </summary>
/// <remarks>
/// Set <see cref="IsDesignTime"/> to <c>true</c> when the screen editor preview is
/// active. Any system that should behave differently at design time — animations,
/// data-binding evaluation, event subscriptions — should check this flag before
/// running runtime logic.
///
/// Example usage:
/// <code>
/// if (!UIDesignModeContext.IsDesignTime)
/// {
///     SubscribeToRuntimeEvents();
/// }
/// </code>
/// </remarks>
public static class UIDesignModeContext
{
    private static bool _isDesignTime;

    /// <summary>
    /// <c>true</c> while the UI screen editor preview is rendering;
    /// <c>false</c> during normal game execution.
    /// </summary>
    public static bool IsDesignTime => _isDesignTime;

    /// <summary>
    /// Activates design-time mode.
    /// Should be called by the screen editor before building a preview.
    /// </summary>
    public static void EnterDesignTime() => _isDesignTime = true;

    /// <summary>
    /// Deactivates design-time mode.
    /// Should be called when the editor is closed or a runtime context is started.
    /// </summary>
    public static void ExitDesignTime() => _isDesignTime = false;
}
