namespace CasaEngine.Framework.Dialogue.Runtime;

public sealed class DialogueLine
{
    public static DialogueLine Empty { get; } = new(string.Empty);

    public DialogueLine(string text)
        : this(text, string.Empty)
    {
    }

    public DialogueLine(string text, string speaker)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(speaker);

        Text = text;
        Speaker = speaker;
    }

    public string Text { get; }
    public string Speaker { get; }
    public bool IsEmpty => Text.Length == 0 && Speaker.Length == 0;
}