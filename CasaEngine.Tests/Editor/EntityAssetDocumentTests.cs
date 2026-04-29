using CasaEngine.Editor;
using CasaEngine.Editor.History;
using Xunit;

namespace CasaEngine.Tests.Editor;

public class EntityAssetDocumentTests
{
    [Fact]
    public void FromDocument_WithEntityDocument_ReturnsEntityHistoryContext()
    {
        var document = new EditorDocumentContext(
            EditorDocumentKind.Entity,
            "panel_entity_asset_deadbeef",
            "Box",
            null);

        var context = EditorHistoryContext.FromDocument(document);

        Assert.Equal(EditorHistoryContextKind.Entity, context.Kind);
        Assert.Equal("panel_entity_asset_deadbeef", context.Id);
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public void FromDocument_WithEntityDocumentWithoutId_ReturnsEmptyContext()
    {
        var document = new EditorDocumentContext(
            EditorDocumentKind.Entity,
            string.Empty,
            "Box",
            null);

        var context = EditorHistoryContext.FromDocument(document);

        Assert.Equal(EditorHistoryContext.Empty, context);
    }
}