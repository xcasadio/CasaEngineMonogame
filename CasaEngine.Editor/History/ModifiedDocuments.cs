using System;
using System.Collections.Generic;

namespace CasaEngine.Editor.History;

/// <summary>
/// Pure helpers over a list of open documents: the titles of the modified ones, and saving all of them.
/// </summary>
internal static class ModifiedDocuments
{
    /// <summary>
    /// For each modified document, in the order received, <c>"&lt;Kind&gt;: &lt;title&gt;"</c>.
    /// </summary>
    public static IReadOnlyList<string> GetModifiedTitles(IReadOnlyList<EditorSavableDocument> documents)
    {
        if (documents == null)
        {
            throw new ArgumentNullException(nameof(documents));
        }

        List<string> titles = new();
        for (int i = 0; i < documents.Count; i++)
        {
            EditorSavableDocument document = documents[i];
            if (document.IsModified())
            {
                titles.Add($"{document.Kind}: {document.Title}");
            }
        }

        return titles;
    }

    /// <summary>
    /// Saves each modified document, goes on after a failure, and returns true only when no document is modified after the
    /// pass.
    /// </summary>
    public static bool TrySaveAll(IReadOnlyList<EditorSavableDocument> documents)
    {
        if (documents == null)
        {
            throw new ArgumentNullException(nameof(documents));
        }

        for (int i = 0; i < documents.Count; i++)
        {
            EditorSavableDocument document = documents[i];
            if (document.IsModified())
            {
                document.TrySave();
            }
        }

        for (int i = 0; i < documents.Count; i++)
        {
            if (documents[i].IsModified())
            {
                return false;
            }
        }

        return true;
    }
}
