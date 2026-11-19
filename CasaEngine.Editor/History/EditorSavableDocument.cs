using System;

namespace CasaEngine.Editor.History;

/// <summary>
/// One open editor document seen the same way by every "save before losing changes?" path (tab close, quit, project
/// switch): which panel it is, what kind of document, its title, whether it is modified and how to save it. Immutable and
/// free of MonoGame and MGUI, so the paths that use it are tested without the editor.
/// </summary>
internal sealed class EditorSavableDocument
{
    private readonly Func<bool> _isModified;
    private readonly Func<bool> _trySave;

    /// <param name="panelId">The identifier of the document's panel.</param>
    /// <param name="kind">The label of the kind of document, such as <c>Screen</c> or <c>Material</c>.</param>
    /// <param name="title">The title of the document, without any modified marker.</param>
    /// <param name="isModified">Whether the document is modified right now.</param>
    /// <param name="trySave">Saves the document; true when it is saved.</param>
    public EditorSavableDocument(string panelId, string kind, string title, Func<bool> isModified, Func<bool> trySave)
    {
        PanelId = panelId ?? throw new ArgumentNullException(nameof(panelId));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        _isModified = isModified ?? throw new ArgumentNullException(nameof(isModified));
        _trySave = trySave ?? throw new ArgumentNullException(nameof(trySave));
    }

    public string PanelId { get; }

    public string Kind { get; }

    public string Title { get; }

    public bool IsModified() => _isModified();

    public bool TrySave() => _trySave();
}
