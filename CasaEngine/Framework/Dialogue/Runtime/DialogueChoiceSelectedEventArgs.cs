namespace CasaEngine.Framework.Dialogue.Runtime;

/// <summary>
/// Raised once a choice list shown via <see cref="IDialoguePresenter"/>-style choice state
/// has been resolved to a single selection.
/// </summary>
public sealed class DialogueChoiceSelectedEventArgs : EventArgs
{
    public DialogueChoiceSelectedEventArgs(int selectedIndex, IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        SelectedIndex = selectedIndex;
        Labels = labels;
    }

    public int SelectedIndex { get; }
    public IReadOnlyList<string> Labels { get; }
}
