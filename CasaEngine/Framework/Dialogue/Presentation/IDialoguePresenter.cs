using CasaEngine.Framework.Dialogue.Runtime;

namespace CasaEngine.Framework.Dialogue.Presentation;

public interface IDialoguePresenter
{
    DialogueRuntimeState State { get; }
    DialogueLine CurrentLine { get; }
    bool IsOpen { get; }

    /// <summary>
    /// The labels of the choice list currently displayed, or an empty list when no choice
    /// list is active (<see cref="State"/> is not <see cref="DialogueRuntimeState.AwaitingChoice"/>).
    /// </summary>
    IReadOnlyList<string> Choices { get; }

    /// <summary>Shorthand for <c>Choices.Count &gt; 0</c>.</summary>
    bool HasChoices { get; }

    event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged;

    /// <summary>
    /// Raised once <see cref="SelectChoice(int)"/> resolves the currently displayed choice list.
    /// </summary>
    event EventHandler<DialogueChoiceSelectedEventArgs> ChoiceSelected;

    bool ShowLine(DialogueLine line);

    /// <summary>
    /// Presents a choice list generically (no domain semantics attached to the labels).
    /// The dialogue is considered open and its state becomes
    /// <see cref="DialogueRuntimeState.AwaitingChoice"/> until <see cref="SelectChoice(int)"/>
    /// is called or the dialogue is closed.
    /// </summary>
    /// <param name="labels">The choice labels to display, in order. Must contain at least one entry.</param>
    bool ShowChoices(IReadOnlyList<string> labels);

    /// <summary>
    /// Called by the UI to report which choice the player selected while
    /// <see cref="State"/> is <see cref="DialogueRuntimeState.AwaitingChoice"/>.
    /// Raises <see cref="ChoiceSelected"/> and clears the choice state.
    /// </summary>
    /// <param name="index">Zero-based index into the labels passed to <see cref="ShowChoices(IReadOnlyList{string})"/>.</param>
    bool SelectChoice(int index);

    bool Close();
}
