namespace CasaEngine.EditorServices.Materials;

public sealed class MaterialPropertySectionDescriptor
{
    public MaterialPropertySectionDescriptor(
        string key,
        string displayName,
        int displayOrder,
        IReadOnlyList<MaterialPropertyDescriptor> properties)
    {
        Key = string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Section key must not be empty.", nameof(key))
            : key;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName;
        DisplayOrder = displayOrder;
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    public string Key { get; }

    public string DisplayName { get; }

    public int DisplayOrder { get; }

    public IReadOnlyList<MaterialPropertyDescriptor> Properties { get; }
}