namespace CasaEngine.Framework.Dialogue.Runtime;

public enum DialogueRuntimeState
{
    Closed,
    Open,

    /// <summary>
    /// The dialogue box is open and a choice list is currently displayed, awaiting the
    /// UI to report the selected index via <see cref="Presentation.IDialoguePresenter.SelectChoice(int)"/>.
    /// </summary>
    AwaitingChoice,
}