namespace CasaEngine.Framework.Dialogue.Runtime;

/// <summary>
/// A single markup attribute (for example <c>[b]bold[/b]</c> or a self-closing <c>[br/]</c>) parsed
/// out of a <see cref="DialogueLine"/>'s text. <see cref="Position"/> and <see cref="Length"/> refer
/// to <see cref="DialogueLine.Text"/>, the already-cleaned text: the markup syntax itself has been
/// removed, and a presenter uses these to style or otherwise react to the covered range.
/// </summary>
public sealed class DialogueMarkupAttribute
{
    public DialogueMarkupAttribute(string name, int position, int length, IReadOnlyDictionary<string, object> properties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(properties);

        Name = name;
        Position = position;
        Length = length;
        Properties = properties;
    }

    /// <summary>The attribute's name, e.g. <c>"b"</c> for <c>[b]...[/b]</c>.</summary>
    public string Name { get; }

    /// <summary>The zero-based start of the covered range, in <see cref="DialogueLine.Text"/>.</summary>
    public int Position { get; }

    /// <summary>The length of the covered range, in <see cref="DialogueLine.Text"/>. Zero for a self-closing marker.</summary>
    public int Length { get; }

    /// <summary>
    /// The attribute's properties (e.g. <c>value</c> on <c>[select value=1 .../]</c>), each a
    /// <see cref="string"/>, <see cref="int"/>, <see cref="float"/> or <see cref="bool"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; }
}
