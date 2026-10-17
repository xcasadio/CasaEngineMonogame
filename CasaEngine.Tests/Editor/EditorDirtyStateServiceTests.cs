using CasaEngine.Editor.History;
using CasaEngine.EditorServices.History;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// Clearing a document's undo history (the script reload clears the World history and keeps the world's edits) does not
/// change whether the document is modified: a modified document stays modified until it is saved, an unmodified one stays
/// unmodified.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorDirtyStateServiceTests : IDisposable
{
    private static readonly EditorHistoryContext Context = new(EditorHistoryContextKind.World, "editor-dirty-state-service-tests");

    public EditorDirtyStateServiceTests()
    {
        EditorHistoryService.Current.IsSuspended = false;
        EditorHistoryService.Current.Remove(Context);
        EditorDirtyStateService.Current.Remove(Context);
    }

    public void Dispose()
    {
        EditorHistoryService.Current.Remove(Context);
        EditorDirtyStateService.Current.Remove(Context);
    }

    private static void Edit() =>
        EditorHistoryService.Current.Execute(Context, new EditorDelegateCommand("Edit", static () => { }, static () => { }));

    private static void ClearHistory() => EditorHistoryService.Current.Clear(Context);

    private static void Undo() => EditorHistoryService.Current.GetOrCreate(Context).Undo();

    private static bool IsDirty => EditorDirtyStateService.Current.IsDirty(Context);

    [Fact]
    public void ANeverSavedModifiedDocument_StaysModified_WhenItsHistoryIsCleared()
    {
        Edit();
        Assert.True(IsDirty);

        ClearHistory();

        Assert.True(IsDirty);
    }

    [Fact]
    public void ASavedUnmodifiedDocument_StaysUnmodified_WhenItsHistoryIsCleared()
    {
        Edit();
        Edit();
        EditorDirtyStateService.Current.MarkSaved(Context);
        Assert.False(IsDirty);

        ClearHistory();

        Assert.False(IsDirty);
    }

    [Fact]
    public void ADocumentModifiedAfterItsSave_StaysModified_WhenItsHistoryIsCleared()
    {
        Edit();
        EditorDirtyStateService.Current.MarkSaved(Context);
        Edit();
        Assert.True(IsDirty);

        ClearHistory();

        Assert.True(IsDirty);
    }

    [Fact]
    public void AnUntouchedDocument_StaysUnmodified_WhenItsHistoryIsCleared()
    {
        ClearHistory();

        Assert.False(IsDirty);
    }

    [Fact]
    public void AfterAClearThatKeptItModified_UndoingTheNextEdit_LeavesTheDocumentModified()
    {
        Edit();
        ClearHistory();

        Edit();
        Undo();

        // The edits made before the clear are still unsaved.
        Assert.True(IsDirty);
    }

    [Fact]
    public void AfterAClearThatKeptItUnmodified_UndoingTheNextEdit_LeavesTheDocumentUnmodified()
    {
        Edit();
        EditorDirtyStateService.Current.MarkSaved(Context);
        ClearHistory();

        Edit();
        Assert.True(IsDirty);
        Undo();

        Assert.False(IsDirty);
    }

    [Fact]
    public void AfterAClearThatKeptItModified_SavingMakesItUnmodified_AndANewEditModifiesItAgain()
    {
        Edit();
        ClearHistory();

        EditorDirtyStateService.Current.MarkSaved(Context);
        Assert.False(IsDirty);

        Edit();
        Assert.True(IsDirty);
        Undo();
        Assert.False(IsDirty);
    }

    [Fact]
    public void ClearingThenMarkingSaved_AsAReopenedDocumentDoes_LeavesItUnmodified()
    {
        Edit();

        ClearHistory();
        EditorDirtyStateService.Current.MarkSaved(Context);

        Assert.False(IsDirty);
    }

    [Fact]
    public void TheClearNotification_ReportsTheKeptModifiedState()
    {
        Edit();
        EditorDirtyStateChangedEventArgs? raised = null;
        void OnDirtyStateChanged(object? sender, EditorDirtyStateChangedEventArgs e)
        {
            if (e.Context.Equals(Context))
            {
                raised = e;
            }
        }

        EditorDirtyStateService.Current.DirtyStateChanged += OnDirtyStateChanged;
        try
        {
            ClearHistory();
        }
        finally
        {
            EditorDirtyStateService.Current.DirtyStateChanged -= OnDirtyStateChanged;
        }

        Assert.NotNull(raised);
        Assert.Equal(EditorDirtyStateChangeKind.Clear, raised!.ChangeKind);
        Assert.True(raised.IsDirty);
    }
}
