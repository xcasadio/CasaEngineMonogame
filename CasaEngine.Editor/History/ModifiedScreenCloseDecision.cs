using System;

namespace CasaEngine.Editor.History;

/// <summary>
/// Bound screens program, phase 8, T4.5 (D17): pure decision for whether a user close (one screen
/// document) or the editor's exit (any number of modified screen documents) may proceed, or must be
/// cancelled to avoid losing unsaved changes. Used identically by both callers: a single screen close
/// asks about that one screen; an exit asks about the whole set at once (the caller lists their titles
/// in a single prompt) and treats "any of them still modified" as the input here.
/// </summary>
public static class ModifiedScreenCloseDecision
{
    public enum Outcome
    {
        /// <summary>The close/exit may proceed.</summary>
        Proceed,

        /// <summary>The close/exit must be cancelled: the caller keeps the document(s) open/the editor running.</summary>
        Cancel,
    }

    /// <summary>The user's answer to the "save before losing changes?" prompt.</summary>
    public enum Answer
    {
        Yes,
        No,
        Cancel,
    }

    public readonly record struct Result(Outcome Outcome, bool SaveAttempted)
    {
        public bool ShouldProceed => Outcome == Outcome.Proceed;
    }

    /// <summary>
    /// Decides whether to proceed. Rules (D17):
    /// <list type="bullet">
    /// <item><paramref name="isModified"/> false: proceed, nothing asked.</item>
    /// <item><paramref name="isAutomationActive"/> true: proceed, nothing asked (the caller logs the
    /// abandoned screen(s) itself - this helper never logs).</item>
    /// <item>Otherwise <paramref name="askUser"/> is called exactly once. Yes: <paramref name="trySave"/>
    /// is called exactly once - success proceeds, failure cancels (so unsaved changes are not lost). No:
    /// proceed without saving. Cancel: cancel.</item>
    /// </list>
    /// <paramref name="trySave"/> is never called unless the user answered Yes.
    /// </summary>
    public static Result Decide(bool isModified, bool isAutomationActive, Func<Answer> askUser, Func<bool> trySave)
    {
        if (!isModified || isAutomationActive)
        {
            return new Result(Outcome.Proceed, SaveAttempted: false);
        }

        if (askUser == null)
        {
            throw new ArgumentNullException(nameof(askUser));
        }

        Answer answer = askUser();
        switch (answer)
        {
            case Answer.Yes:
                if (trySave == null)
                {
                    throw new ArgumentNullException(nameof(trySave));
                }

                bool saved = trySave();
                return new Result(saved ? Outcome.Proceed : Outcome.Cancel, SaveAttempted: true);

            case Answer.No:
                return new Result(Outcome.Proceed, SaveAttempted: false);

            default:
                return new Result(Outcome.Cancel, SaveAttempted: false);
        }
    }
}
