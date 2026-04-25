namespace CasaEngine.EditorServices.ScreenEditor.Inspector;

/// <summary>Metadata describing a single editable property in the UI screen inspector.</summary>
public sealed class UIPropertyDescriptor
{
    /// <summary>Name as stored in <see cref="DocumentModel.UIScreenPropertyValue.Name"/>.</summary>
    public string Name { get; }

    /// <summary>Human-readable label shown in the inspector.</summary>
    public string DisplayName { get; }

    /// <summary>Optional grouping label (e.g. "Layout", "Appearance").</summary>
    public string Category { get; }

    /// <summary>CLR type of the property value (string, int, float, enum, …).</summary>
    public Type ValueType { get; }

    /// <summary>Default serialized value used when the property is not yet set on a node.</summary>
    public string? DefaultSerializedValue { get; }

    /// <summary>When false, the property is shown read-only in the inspector.</summary>
    public bool IsEditable { get; }

    public UIPropertyDescriptor(
        string name,
        string displayName,
        string category,
        Type valueType,
        string? defaultSerializedValue = null,
        bool isEditable = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name must not be empty.", nameof(name));
        }

        Name = name;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        DefaultSerializedValue = defaultSerializedValue;
        IsEditable = isEditable;
    }
}
