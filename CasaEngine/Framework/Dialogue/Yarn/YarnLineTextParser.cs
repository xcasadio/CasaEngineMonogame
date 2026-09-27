using CasaEngine.Framework.Dialogue.Runtime;

namespace CasaEngine.Framework.Dialogue.Yarn;

/// <summary>
/// Parses a raw Yarn line's text into a <see cref="DialogueLine"/>: substitutions expanded, markup
/// analyzed, the <c>character</c> attribute resolved into <see cref="DialogueLine.Speaker"/> and
/// removed from the text, the remaining markup carried as <see cref="DialogueLine.Attributes"/>.
/// This is the same pipeline <see cref="YarnDialogueRunner"/> applies to each line of a running
/// dialogue, exposed here as a static utility for a line a game shows outside of a dialogue box (a
/// menu, an inventory), typically read with <see cref="Assets.DialogueAsset.TryGetLineText"/>.
/// </summary>
public static class YarnLineTextParser
{
    private static readonly global::Yarn.Markup.LineParser LineParser = CreateMarkupParser();

    /// <summary>
    /// Replaces <c>{0}</c>-style substitution markers in <paramref name="rawText"/> with
    /// <paramref name="substitutions"/>, the same way a running dialogue's line text is expanded.
    /// </summary>
    public static string ExpandSubstitutions(string rawText, IList<string> substitutions)
    {
        ArgumentNullException.ThrowIfNull(rawText);
        ArgumentNullException.ThrowIfNull(substitutions);

        return global::Yarn.Markup.LineParser.ExpandSubstitutions(rawText, substitutions);
    }

    /// <summary>
    /// Parses <paramref name="rawText"/> (already substituted, e.g. by <see cref="ExpandSubstitutions"/>)
    /// and returns the resulting <see cref="DialogueLine"/>. Malformed markup never throws: the line is
    /// returned with <paramref name="rawText"/> unchanged as its text, no speaker and no attributes.
    /// </summary>
    public static DialogueLine Parse(string rawText, string localeCode = "en")
    {
        ArgumentNullException.ThrowIfNull(rawText);
        ArgumentException.ThrowIfNullOrWhiteSpace(localeCode);

        try
        {
            global::Yarn.Markup.MarkupParseResult parsed = LineParser.ParseString(rawText, localeCode);
            string speaker = string.Empty;

            if (parsed.TryGetAttributeWithName(global::Yarn.Markup.LineParser.CharacterAttribute, out global::Yarn.Markup.MarkupAttribute characterAttribute))
            {
                if (characterAttribute.TryGetProperty(global::Yarn.Markup.LineParser.CharacterAttributeNameProperty, out string characterName))
                {
                    speaker = characterName;
                }

                parsed = parsed.DeleteRange(characterAttribute);
            }

            return new DialogueLine(parsed.Text, speaker, BuildAttributes(parsed.Attributes));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"YarnLineTextParser: failed to parse markup: {exception.Message}");
            return new DialogueLine(rawText);
        }
    }

    internal static List<DialogueMarkupAttribute> BuildAttributes(IReadOnlyList<global::Yarn.Markup.MarkupAttribute> source)
    {
        var attributes = new List<DialogueMarkupAttribute>(source.Count);
        foreach (global::Yarn.Markup.MarkupAttribute attribute in source)
        {
            var properties = new Dictionary<string, object>(attribute.Properties.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, global::Yarn.Markup.MarkupValue> property in attribute.Properties)
            {
                properties[property.Key] = property.Value.Type switch
                {
                    global::Yarn.Markup.MarkupValueType.String => property.Value.StringValue,
                    global::Yarn.Markup.MarkupValueType.Integer => property.Value.IntegerValue,
                    global::Yarn.Markup.MarkupValueType.Float => property.Value.FloatValue,
                    global::Yarn.Markup.MarkupValueType.Bool => property.Value.BoolValue,
                    _ => property.Value.StringValue,
                };
            }

            attributes.Add(new DialogueMarkupAttribute(attribute.Name, attribute.Position, attribute.Length, properties));
        }

        return attributes;
    }

    internal static global::Yarn.Markup.LineParser CreateMarkupParser()
    {
        var parser = new global::Yarn.Markup.LineParser();
        var builtInReplacer = new global::Yarn.Markup.BuiltInMarkupReplacer();
        parser.RegisterMarkerProcessor("select", builtInReplacer);
        parser.RegisterMarkerProcessor("plural", builtInReplacer);
        parser.RegisterMarkerProcessor("ordinal", builtInReplacer);
        return parser;
    }
}
