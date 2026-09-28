namespace CasaEngine.Framework.Dialogue.Runtime;

public sealed class DialogueLine
{
    private static readonly IReadOnlyList<DialogueMarkupAttribute> NoAttributes = Array.Empty<DialogueMarkupAttribute>();

    public static DialogueLine Empty { get; } = new(string.Empty);

    public DialogueLine(string text)
        : this(text, string.Empty, NoAttributes)
    {
    }

    public DialogueLine(string text, string speaker)
        : this(text, speaker, NoAttributes)
    {
    }

    /// <summary>
    /// Creates a line carrying the markup attributes parsed out of its text (e.g. by
    /// <c>YarnDialogueRunner</c>), for a presenter that wants to style or otherwise react to them.
    /// </summary>
    public DialogueLine(string text, string speaker, IReadOnlyList<DialogueMarkupAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(attributes);

        Text = text;
        Speaker = speaker;
        Attributes = attributes;
    }

    public string Text { get; }
    public string Speaker { get; }

    /// <summary>The markup attributes parsed out of <see cref="Text"/>. Empty unless the source parsed markup.</summary>
    public IReadOnlyList<DialogueMarkupAttribute> Attributes { get; }
    public bool IsEmpty => Text.Length == 0 && Speaker.Length == 0;
}