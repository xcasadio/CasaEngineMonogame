using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.History;

/// <summary>
/// The asynchronous "save before quitting?" question of screen documents (ADR-0041, plan task T3.2). The game needs to know
/// in <c>OnExiting</c> whether the exit goes on, but the answer to an MGUI message box arrives later: an exit that needs an
/// answer is cancelled now, the question is asked once, and the exit is requested again once the answer lets it go - that
/// second exit is let through without asking. Pure: the modified screens, the question, the save and the exit request are
/// injected, so every path is tested without MonoGame or MGUI.
/// </summary>
internal sealed class ModifiedScreensExitCoordinator
{
    private readonly Func<IReadOnlyList<string>> _modifiedScreenTitles;
    private readonly Func<bool> _isAutomationActive;
    private readonly Action<IReadOnlyList<string>> _logAbandonedUnderAutomation;
    private readonly Action<IReadOnlyList<string>, Action<ModifiedScreenCloseDecision.Answer>> _ask;
    private readonly Func<bool> _trySaveAll;
    private readonly Action _requestExit;
    private bool _exitConfirmed;
    private bool _questionPending;

    /// <param name="modifiedScreenTitles">The titles of the screen documents modified right now.</param>
    /// <param name="isAutomationActive">Whether editor automation runs: then nothing is asked and the abandoned screens are
    /// logged instead, as before (D9).</param>
    /// <param name="logAbandonedUnderAutomation">Logs the screens an automated exit abandons.</param>
    /// <param name="ask">Asks the question about the listed screens and calls the given callback with the answer, once.</param>
    /// <param name="trySaveAll">Saves every modified screen; false when one is still modified afterwards.</param>
    /// <param name="requestExit">Requests the exit again (<c>Game.Exit</c>), which calls <see cref="OnExiting"/> again.</param>
    public ModifiedScreensExitCoordinator(
        Func<IReadOnlyList<string>> modifiedScreenTitles,
        Func<bool> isAutomationActive,
        Action<IReadOnlyList<string>> logAbandonedUnderAutomation,
        Action<IReadOnlyList<string>, Action<ModifiedScreenCloseDecision.Answer>> ask,
        Func<bool> trySaveAll,
        Action requestExit)
    {
        _modifiedScreenTitles = modifiedScreenTitles ?? throw new ArgumentNullException(nameof(modifiedScreenTitles));
        _isAutomationActive = isAutomationActive ?? throw new ArgumentNullException(nameof(isAutomationActive));
        _logAbandonedUnderAutomation = logAbandonedUnderAutomation ?? throw new ArgumentNullException(nameof(logAbandonedUnderAutomation));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
        _trySaveAll = trySaveAll ?? throw new ArgumentNullException(nameof(trySaveAll));
        _requestExit = requestExit ?? throw new ArgumentNullException(nameof(requestExit));
    }

    /// <summary>True while the quit question waits for its answer.</summary>
    public bool IsQuestionPending => _questionPending;

    /// <summary>
    /// Called from <c>Game.OnExiting</c>. Returns true when the exit must be cancelled now: screens are modified outside
    /// automation, and the question is asked (or was already asked and still waits - never twice). An exit requested again
    /// after an answer that lets it go (Save with every screen saved, or Don't Save) is let through.
    /// </summary>
    public bool OnExiting()
    {
        if (_exitConfirmed)
        {
            //  One exit only: should that exit not happen after all, the next one asks again.
            _exitConfirmed = false;
            return false;
        }

        IReadOnlyList<string> titles = _modifiedScreenTitles();
        if (titles.Count == 0)
        {
            return false;
        }

        if (_isAutomationActive())
        {
            _logAbandonedUnderAutomation(titles);
            return false;
        }

        if (!_questionPending)
        {
            _questionPending = true;
            _ask(titles, OnAnswered);
        }

        return true;
    }

    private void OnAnswered(ModifiedScreenCloseDecision.Answer answer)
    {
        if (!_questionPending)
        {
            return; // a second answer to the same question
        }

        _questionPending = false;
        var result = ModifiedScreenCloseDecision.ApplyAnswer(answer, _trySaveAll);
        if (!result.ShouldProceed)
        {
            return;
        }

        _exitConfirmed = true;
        _requestExit();
    }
}
