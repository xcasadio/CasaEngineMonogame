using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.History;

/// <summary>
/// The "ask, then go on" gate of an action that would lose modified documents (quit, open another project). The answer to an
/// MGUI message box arrives later, so the action is handed to <see cref="Request"/> and runs at once when nothing needs
/// asking, or after the answer when it lets the action go. Pure: the modified titles, the question and the save are injected.
/// </summary>
internal sealed class ModifiedDocumentsGate
{
    private readonly Func<IReadOnlyList<string>> _modifiedTitles;
    private readonly Func<bool> _isAutomationActive;
    private readonly Action<IReadOnlyList<string>, Action<ModifiedScreenCloseDecision.Answer>> _ask;
    private readonly Func<bool> _trySaveAll;
    private Action _pendingProceed;
    private bool _questionPending;

    /// <param name="modifiedTitles">The titles of the documents modified right now.</param>
    /// <param name="isAutomationActive">Whether editor automation runs: then nothing is asked.</param>
    /// <param name="ask">Asks the question about the listed documents and calls the callback with the answer, once.</param>
    /// <param name="trySaveAll">Saves every modified document; false when one is still modified afterwards.</param>
    public ModifiedDocumentsGate(
        Func<IReadOnlyList<string>> modifiedTitles,
        Func<bool> isAutomationActive,
        Action<IReadOnlyList<string>, Action<ModifiedScreenCloseDecision.Answer>> ask,
        Func<bool> trySaveAll)
    {
        _modifiedTitles = modifiedTitles ?? throw new ArgumentNullException(nameof(modifiedTitles));
        _isAutomationActive = isAutomationActive ?? throw new ArgumentNullException(nameof(isAutomationActive));
        _ask = ask ?? throw new ArgumentNullException(nameof(ask));
        _trySaveAll = trySaveAll ?? throw new ArgumentNullException(nameof(trySaveAll));
    }

    /// <summary>True while the question waits for its answer.</summary>
    public bool IsQuestionPending => _questionPending;

    /// <summary>
    /// Runs <paramref name="proceed"/> at once when no document is modified or automation runs. Otherwise asks, unless a
    /// question already waits (then the request is ignored), and runs it once if the answer lets the action go.
    /// </summary>
    public void Request(Action proceed)
    {
        if (proceed == null)
        {
            throw new ArgumentNullException(nameof(proceed));
        }

        if (_questionPending)
        {
            return;
        }

        IReadOnlyList<string> titles = _modifiedTitles();
        if (titles.Count == 0 || _isAutomationActive())
        {
            proceed();
            return;
        }

        _questionPending = true;
        _pendingProceed = proceed;
        _ask(titles, OnAnswered);
    }

    private void OnAnswered(ModifiedScreenCloseDecision.Answer answer)
    {
        if (!_questionPending)
        {
            return; // a second answer to the same question
        }

        _questionPending = false;
        Action proceed = _pendingProceed;
        _pendingProceed = null;

        var result = ModifiedScreenCloseDecision.ApplyAnswer(answer, _trySaveAll);
        if (result.ShouldProceed)
        {
            proceed();
        }
    }
}
