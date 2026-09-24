using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Xaml;

namespace CasaEngine.EditorServices.ScreenEditor.Session;

/// <summary>
/// Writes a <see cref="UIScreenDocument"/> back to its source XAML file, applying the same lossless-save
/// decision <see cref="UIScreenEditorSession.Save"/> used to make on its own (T4.1, engine ADR-0038
/// "Lossless editor round trip"): replay the original bytes verbatim when nothing semantically changed
/// since the document was parsed, otherwise serialize through the fidelity serializer and re-apply the
/// source file's original byte-order mark. Extracted out of the session (T4.4, D13) so another editor
/// surface -- <see cref="EditorServices.ScreenEditor"/> and, through it, the editor's preview panel and
/// its "File &gt; Save" -- can write a document with the exact same guarantees, without a
/// <see cref="UIScreenEditorSession"/> of its own.
/// </summary>
public static class UIScreenDocumentFileWriter
{
    /// <summary>
    /// Writes <paramref name="document"/> to <paramref name="sourceXamlFilePath"/>: a byte-identical
    /// replay of <see cref="UIScreenDocument.OriginalBytes"/> when the document did not semantically
    /// change since it was parsed, otherwise a fresh serialization through <paramref name="serializer"/>
    /// encoded with the source file's original byte-order mark. Updates
    /// <see cref="UIScreenDocument.OriginalBytes"/> and <see cref="UIScreenDocument.BaselineSemanticSnapshot"/>
    /// so a later unmodified write takes the byte-identical fast path too.
    /// </summary>
    public static void Write(UIScreenDocument document, string sourceXamlFilePath, UIScreenXamlSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceXamlFilePath);
        ArgumentNullException.ThrowIfNull(serializer);

        // Whether the document actually changed since it was opened is decided from a semantic snapshot
        // comparison, not from a caller-tracked dirty flag alone (T4.1, engine ADR-0038 "Lossless editor
        // round trip"): trusting a dirty flag alone could rewrite a byte-identical file through the
        // serializer and lose formatting it does not model.
        var currentSnapshot = UIScreenSemanticSnapshot.Compute(document);
        if (document.OriginalBytes != null
            && string.Equals(currentSnapshot, document.BaselineSemanticSnapshot, StringComparison.Ordinal))
        {
            File.WriteAllBytes(sourceXamlFilePath, document.OriginalBytes);
        }
        else
        {
            var serialized = serializer.Serialize(document);
            var bytes = EncodeWithOriginalBom(serialized, document.OriginalBytes);
            File.WriteAllBytes(sourceXamlFilePath, bytes);

            // The file on disk now matches this state exactly: keep the document's own bookkeeping in
            // sync so a later unmodified write (no further edits) takes the byte-identical fast path too.
            document.OriginalBytes = bytes;
            document.BaselineSemanticSnapshot = currentSnapshot;
        }
    }

    /// <summary>
    /// Encodes <paramref name="text"/> as UTF-8, reproducing the presence or absence of a byte-order mark
    /// from <paramref name="originalBytes"/> (or omitting it, matching the previous plain
    /// <see cref="File.WriteAllText(string, string)"/> behaviour, when there is no original file to match).
    /// </summary>
    private static byte[] EncodeWithOriginalBom(string text, byte[]? originalBytes)
    {
        var hasBom = originalBytes is { Length: >= 3 }
            && originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF;

        // Encoding.GetBytes never writes the preamble, whatever encoderShouldEmitUTF8Identifier says: prepend it.
        var body = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        if (!hasBom)
        {
            return body;
        }

        var bytes = new byte[body.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        body.CopyTo(bytes, 3);
        return bytes;
    }
}
