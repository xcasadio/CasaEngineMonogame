namespace CasaEngine.Editor;

public sealed record EditorDocumentContext(EditorDocumentKind Kind, string Id, string DisplayName, object? Payload = null)
{
    public static EditorDocumentContext Empty { get; } = new(EditorDocumentKind.None, string.Empty, string.Empty);
}