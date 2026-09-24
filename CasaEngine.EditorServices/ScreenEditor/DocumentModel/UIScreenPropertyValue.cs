namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

public sealed class UIScreenPropertyValue
{
    public UIScreenPropertyValue(string name, string? serializedValue, string valueType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name cannot be null or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(valueType))
        {
            throw new ArgumentException("Value type cannot be null or whitespace.", nameof(valueType));
        }

        Name = name;
        SerializedValue = serializedValue;
        ValueType = valueType;
    }

    public string Name { get; }

    public string? SerializedValue { get; private set; }

    public string ValueType { get; private set; }

    /// <summary>
    /// Optional binding expression.  When set, the property serializes as
    /// <c>{Binding Path}</c> rather than the literal <see cref="SerializedValue"/>.
    /// </summary>
    public UIScreenBindingValue? Binding { get; set; }

    /// <summary>Returns the effective serialized string: the binding markup if a binding is set, otherwise the literal value.</summary>
    public string? EffectiveSerializedValue
        => Binding != null ? Binding.ToMarkupString() : SerializedValue;

    /// <summary>
    /// For a property that was parsed from a XAML property element (<see cref="ValueType"/> "xaml"), the
    /// original owner-qualified element local name exactly as authored (for example <c>Canvas.Left</c> or
    /// <c>Window.Resources</c>). The serializer uses this verbatim instead of reconstructing
    /// <c>{node.ControlType}.{Name}</c>, which is wrong for an attached property whose owner differs from
    /// the node it is set on. Null for a property that has no XAML source (a brand-new property, or one on
    /// a document created without source text).
    /// </summary>
    public string? ElementQualifiedName { get; internal set; }

    /// <summary>
    /// The live XAML property element this value was parsed from, kept so the fidelity serializer (T4.1,
    /// engine ADR-0038) can patch it in place -- leaving it untouched when its content did not change, and
    /// preserving every comment, namespace and surrounding whitespace it and its siblings carry. Null when
    /// there is no source document to patch (a brand-new property, or a document created without source text).
    /// </summary>
    internal System.Xml.Linq.XElement? SourceElement { get; set; }

    public void SetValue(string? serializedValue, string valueType)
    {
        if (string.IsNullOrWhiteSpace(valueType))
        {
            throw new ArgumentException("Value type cannot be null or whitespace.", nameof(valueType));
        }

        SerializedValue = serializedValue;
        ValueType = valueType;

        // If the new value looks like a binding, parse and store it
        var binding = UIScreenBindingValue.TryParse(serializedValue);
        if (binding != null)
        {
            Binding = binding;
            SerializedValue = null; // binding takes precedence
        }
    }
}