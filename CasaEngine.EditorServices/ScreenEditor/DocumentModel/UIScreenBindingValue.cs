namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

/// <summary>
/// Represents a data-binding expression stored on a <see cref="UIScreenPropertyValue"/>.
/// When present, the property serializes as <c>{Binding Path}</c> rather than a literal value.
/// </summary>
public sealed class UIScreenBindingValue
{
    /// <summary>The binding path expression (e.g. <c>Player.Score</c>).</summary>
    public string BindingPath { get; set; } = string.Empty;

    /// <summary>Optional fallback literal value used at design time when no data context is set.</summary>
    public string? FallbackValue { get; set; }

    /// <summary>Returns the serialized binding markup string, e.g. <c>{Binding Player.Score}</c>.</summary>
    public string ToMarkupString()
        => string.IsNullOrWhiteSpace(BindingPath)
            ? string.Empty
            : $"{{Binding {BindingPath}}}";

    /// <summary>
    /// Tries to parse a binding markup string of the form <c>{Binding Path}</c> or
    /// <c>{Binding}</c>.  Returns null if the string is not a binding expression.
    /// </summary>
    public static UIScreenBindingValue? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("{Binding", StringComparison.OrdinalIgnoreCase) || !trimmed.EndsWith('}'))
        {
            return null;
        }

        var inner = trimmed["{Binding".Length..^1].Trim();
        return new UIScreenBindingValue { BindingPath = inner };
    }
}
