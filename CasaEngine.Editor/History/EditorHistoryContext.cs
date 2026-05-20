using CasaEngine.Editor.Workspaces;

namespace CasaEngine.Editor.History;

public readonly record struct EditorHistoryContext(EditorHistoryContextKind Kind, string Id)
{
    public static EditorHistoryContext Empty { get; } = new(EditorHistoryContextKind.None, string.Empty);

    public static EditorHistoryContext ContentBrowser { get; } = new(EditorHistoryContextKind.ContentBrowser, EditorPanelIds.ContentBrowser);

    public bool IsEmpty => Kind == EditorHistoryContextKind.None || string.IsNullOrWhiteSpace(Id);

    public static EditorHistoryContext FromDocument(EditorDocumentContext? document)
    {
        if (document == null || string.IsNullOrWhiteSpace(document.Id))
        {
            return Empty;
        }

        return document.Kind switch
        {
            EditorDocumentKind.World => new EditorHistoryContext(EditorHistoryContextKind.World, document.Id),
            EditorDocumentKind.UIScreen => new EditorHistoryContext(EditorHistoryContextKind.UIScreen, document.Id),
            EditorDocumentKind.Material => new EditorHistoryContext(EditorHistoryContextKind.Material, document.Id),
            EditorDocumentKind.Entity => new EditorHistoryContext(EditorHistoryContextKind.Entity, document.Id),
            EditorDocumentKind.AnimationClip => new EditorHistoryContext(EditorHistoryContextKind.AnimationClip, document.Id),
            EditorDocumentKind.Particle => new EditorHistoryContext(EditorHistoryContextKind.Particle, document.Id),
            _ => Empty,
        };
    }
}