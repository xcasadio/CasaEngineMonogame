namespace CasaEngine.Editor;

public sealed record EditorSelectionState(EditorSelectionKind Kind, object PrimaryItem, int Count, string Summary)
{
    public static EditorSelectionState Empty { get; } = new(EditorSelectionKind.None, null, 0, "Nothing selected");
}