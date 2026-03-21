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

    public void SetValue(string? serializedValue, string valueType)
    {
        if (string.IsNullOrWhiteSpace(valueType))
        {
            throw new ArgumentException("Value type cannot be null or whitespace.", nameof(valueType));
        }

        SerializedValue = serializedValue;
        ValueType = valueType;
    }
}