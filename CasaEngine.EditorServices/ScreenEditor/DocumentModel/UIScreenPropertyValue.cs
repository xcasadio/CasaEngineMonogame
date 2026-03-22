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