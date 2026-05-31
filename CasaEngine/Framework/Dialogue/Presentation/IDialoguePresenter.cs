using CasaEngine.Framework.Dialogue.Runtime;

namespace CasaEngine.Framework.Dialogue.Presentation;

public interface IDialoguePresenter
{
    DialogueRuntimeState State { get; }
    DialogueLine CurrentLine { get; }
    bool IsOpen { get; }

    event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged;

    bool ShowLine(DialogueLine line);
    bool Close();
}