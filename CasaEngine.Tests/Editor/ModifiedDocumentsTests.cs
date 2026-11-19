using System;
using System.Collections.Generic;
using CasaEngine.Editor.History;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>Editor unsaved documents plan, T1.1: the pure helpers over a list of savable documents.</summary>
public class ModifiedDocumentsTests
{
    private sealed class Doc
    {
        public bool Modified;
        public bool SaveSucceeds = true;
        public int SaveCount;
        public EditorSavableDocument Document { get; }

        public Doc(string kind, string title, bool modified)
        {
            Modified = modified;
            Document = new EditorSavableDocument("panel-" + title, kind, title,
                () => Modified,
                () =>
                {
                    SaveCount++;
                    if (SaveSucceeds)
                    {
                        Modified = false;
                    }

                    return SaveSucceeds;
                });
        }
    }

    private static EditorSavableDocument[] All(params Doc[] docs)
    {
        List<EditorSavableDocument> list = new();
        foreach (Doc doc in docs)
        {
            list.Add(doc.Document);
        }

        return list.ToArray();
    }

    [Fact]
    public void GetModifiedTitles_ListsModifiedDocuments_InOrder_WithTheirKind()
    {
        Doc a = new("Screen", "Menu", true);
        Doc b = new("Material", "Steel", false);
        Doc c = new("World", "Level1", true);

        Assert.Equal(new[] { "Screen: Menu", "World: Level1" }, ModifiedDocuments.GetModifiedTitles(All(a, b, c)));
    }

    [Fact]
    public void GetModifiedTitles_EmptyList_IsEmpty()
    {
        Assert.Empty(ModifiedDocuments.GetModifiedTitles(Array.Empty<EditorSavableDocument>()));
    }

    [Fact]
    public void TrySaveAll_SavesOnlyTheModifiedDocuments_AndReturnsTrue()
    {
        Doc a = new("Screen", "A", true);
        Doc b = new("Sound", "B", false);

        Assert.True(ModifiedDocuments.TrySaveAll(All(a, b)));
        Assert.Equal(1, a.SaveCount);
        Assert.Equal(0, b.SaveCount);
    }

    [Fact]
    public void TrySaveAll_GoesOnAfterAFailure_AndReturnsFalse()
    {
        Doc a = new("Screen", "A", true) { SaveSucceeds = false };
        Doc b = new("Sprite", "B", true);

        Assert.False(ModifiedDocuments.TrySaveAll(All(a, b)));
        Assert.Equal(1, a.SaveCount);
        Assert.Equal(1, b.SaveCount);
        Assert.False(b.Modified);
    }

    [Fact]
    public void TrySaveAll_EmptyList_ReturnsTrue()
    {
        Assert.True(ModifiedDocuments.TrySaveAll(Array.Empty<EditorSavableDocument>()));
    }

    [Fact]
    public void NullArguments_FailEarly()
    {
        Assert.Throws<ArgumentNullException>(() => ModifiedDocuments.GetModifiedTitles(null));
        Assert.Throws<ArgumentNullException>(() => ModifiedDocuments.TrySaveAll(null));
        Assert.Throws<ArgumentNullException>(() => new EditorSavableDocument(null, "k", "t", () => false, () => true));
        Assert.Throws<ArgumentNullException>(() => new EditorSavableDocument("p", null, "t", () => false, () => true));
        Assert.Throws<ArgumentNullException>(() => new EditorSavableDocument("p", "k", null, () => false, () => true));
        Assert.Throws<ArgumentNullException>(() => new EditorSavableDocument("p", "k", "t", null, () => true));
        Assert.Throws<ArgumentNullException>(() => new EditorSavableDocument("p", "k", "t", () => false, null));
    }
}
