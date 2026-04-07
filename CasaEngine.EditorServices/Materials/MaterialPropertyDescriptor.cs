

namespace CasaEngine.EditorServices.Materials;

public sealed class MaterialPropertyDescriptor
{
    public MaterialPropertyDescriptor(
        MaterialPropertyDefinition definition,
        string category,
        string editorControlHint,
        int displayOrder)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Category = string.IsNullOrWhiteSpace(category)
            ? throw new ArgumentException("Category must not be empty.", nameof(category))
            : category;
        EditorControlHint = string.IsNullOrWhiteSpace(editorControlHint)
            ? throw new ArgumentException("Editor control hint must not be empty.", nameof(editorControlHint))
            : editorControlHint;
        DisplayOrder = displayOrder;
    }

    public MaterialPropertyDefinition Definition { get; }

    public string Key => Definition.Key;

    public string DisplayName => Definition.DisplayName;

    public string Category { get; }

    public string EditorControlHint { get; }

    public int DisplayOrder { get; }
}