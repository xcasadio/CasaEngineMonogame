using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.History;

/// <summary>
/// The asynchronous "save before closing?" question for screen documents (ADR-0039, plan decisions D4 and D5). The docking
/// host needs to know at once whether a user close goes on, but the answer to an MGUI message box arrives later: a close
/// that needs an answer is refused now, the question is asked, and the close is redone by code once the answer lets it go.
/// Pure: the dirty state, the question, the save and the close are injected, so every path is tested without MGUI.
/// </summary>
internal sealed class ModifiedScreenCloseCoordinator
{
    private readonly HashSet<string> _pendingPanelIds = new(StringComparer.Ordinal);
    private readonly Func<string, bool> _isModified;
    private readonly Func<bool> _isAutomationActive;
    private readonly Action<string, Action<ModifiedScreenCloseDecision.Answer>> _ask;
    private readonly Func<string, bool> _isScreenOpen;
    private readonly Func<string, bool> _trySave;

    /// <param name="isModified">Whether the screen document of a panel is modified.</param>
    /// <param name="isAutomationActive">Whether editor automation runs: then nothing is asked, as before (D9).</param>
    /// <param name="ask">Asks the question about a panel and calls the given callback with the answer, once.</param>
    /// <param name="isScreenOpen">Whether the panel still shows a screen document when the answer arrives.</param>
    /// <param name="trySave">Saves the panel's screen document; false when it could not be saved.</param>
    public ModifiedScreenCloseCoordinator(
        Func<string, bool> isModified,
        Func<bool> isAutomationActive,
        Action<string, Action<ModifiedScreenCloseDecision.Answer>> ask,
        Func<string, bool> isScreenOpen,
        Func<string, bool> trySave)
    {
        _isModified = isModified ?? throw new ArgumentNullException(nameof(isModified));
        _isAutomationActive = isAutomationActive ?? throw new ArgumentNullException(nameof(isAutomationActive));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
        _isScreenOpen = isScreenOpen ?? throw new ArgumentNullException(nameof(isScreenOpen));
        _trySave = trySave ?? throw new ArgumentNullException(nameof(trySave));
    }

    /// <summary>True while a question about <paramref name="panelId"/> waits for its answer.</summary>
    public bool IsQuestionPending(string panelId) => _pendingPanelIds.Contains(panelId);

    /// <summary>
    /// Called when the user closes the screen panel <paramref name="panelId"/>. Returns true when the close must be refused
    /// now: the screen is modified outside automation, and a question about it is asked (or was already asked and still
    /// waits - never twice). Once the answer lets the screen close, <paramref name="close"/> closes it by code; if that
    /// succeeded, <paramref name="retryWindowClose"/> (non-null only when the close of a whole floating window was refused
    /// for this screen) retries that window close, which asks about its next modified screen (D5). An answer that keeps the
    /// screen, or a save that fails, stops there: nothing is closed and nothing is retried.
    /// </summary>
    public bool OnClosing(string panelId, Func<bool> close, Action retryWindowClose)
    {
        ArgumentNullException.ThrowIfNull(panelId);
        ArgumentNullException.ThrowIfNull(close);

        if (!ModifiedScreenCloseDecision.NeedsAnswer(_isModified(panelId), _isAutomationActive()))
        {
            return false;
        }

        if (_pendingPanelIds.Add(panelId))
        {
            _ask(panelId, answer => OnAnswered(panelId, close, retryWindowClose, answer));
        }

        return true;
    }

    private void OnAnswered(string panelId, Func<bool> close, Action retryWindowClose, ModifiedScreenCloseDecision.Answer answer)
    {
        if (!_pendingPanelIds.Remove(panelId))
        {
            return; // a second answer to the same question
        }

        //  The screen may have gone while the question waited (closed by code, project switched): nothing left to decide.
        if (!_isScreenOpen(panelId))
        {
            return;
        }

        var result = ModifiedScreenCloseDecision.ApplyAnswer(answer, () => _trySave(panelId));
        if (!result.ShouldProceed || !close())
        {
            return;
        }

        retryWindowClose?.Invoke();
    }
}
