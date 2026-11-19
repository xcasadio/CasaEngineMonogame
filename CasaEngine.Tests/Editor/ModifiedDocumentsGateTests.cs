using System;
using System.Collections.Generic;
using CasaEngine.Editor.History;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>Editor unsaved documents plan, T1.1: the "ask, then go on" gate.</summary>
public class ModifiedDocumentsGateTests
{
    private sealed class Editor
    {
        public List<string> Modified { get; } = new();
        public bool Automation { get; set; }
        public bool SaveSucceeds { get; set; } = true;
        public List<IReadOnlyList<string>> Asked { get; } = new();
        public List<Action<ModifiedScreenCloseDecision.Answer>> Answers { get; } = new();
        public int SaveAllCount { get; private set; }
        public int Proceeded { get; private set; }
        public ModifiedDocumentsGate Gate { get; }

        public Editor(params string[] modified)
        {
            Modified.AddRange(modified);
            Gate = new ModifiedDocumentsGate(
                modifiedTitles: () => Modified.ToArray(),
                isAutomationActive: () => Automation,
                ask: (titles, answered) =>
                {
                    Asked.Add(titles);
                    Answers.Add(answered);
                },
                trySaveAll: () =>
                {
                    SaveAllCount++;
                    if (SaveSucceeds)
                    {
                        Modified.Clear();
                    }

                    return SaveSucceeds;
                });
        }

        public void Request() => Gate.Request(() => Proceeded++);

        public void Answer(int question, ModifiedScreenCloseDecision.Answer answer) => Answers[question](answer);
    }

    [Fact]
    public void NothingModified_Proceeds_WithoutAQuestion()
    {
        Editor editor = new();

        editor.Request();

        Assert.Equal(1, editor.Proceeded);
        Assert.Empty(editor.Asked);
    }

    [Fact]
    public void UnderAutomation_Proceeds_WithoutAQuestion()
    {
        Editor editor = new("A") { Automation = true };

        editor.Request();

        Assert.Equal(1, editor.Proceeded);
        Assert.Empty(editor.Asked);
    }

    [Fact]
    public void Modified_Asks_AndDoesNotProceed()
    {
        Editor editor = new("A", "B");

        editor.Request();

        Assert.Equal(new[] { "A", "B" }, Assert.Single(editor.Asked));
        Assert.Equal(0, editor.Proceeded);
        Assert.True(editor.Gate.IsQuestionPending);
    }

    [Fact]
    public void Save_Succeeding_ProceedsOnce()
    {
        Editor editor = new("A");
        editor.Request();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Equal(1, editor.SaveAllCount);
        Assert.Equal(1, editor.Proceeded);
        Assert.False(editor.Gate.IsQuestionPending);
    }

    [Fact]
    public void Save_Failing_DoesNotProceed_AndTheNextRequestAsksAgain()
    {
        Editor editor = new("A") { SaveSucceeds = false };
        editor.Request();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Equal(0, editor.Proceeded);
        Assert.False(editor.Gate.IsQuestionPending);

        editor.Request();
        Assert.Equal(2, editor.Asked.Count);
    }

    [Fact]
    public void DontSave_Proceeds_WithoutSaving()
    {
        Editor editor = new("A");
        editor.Request();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Equal(0, editor.SaveAllCount);
        Assert.Equal(1, editor.Proceeded);
    }

    [Fact]
    public void Cancel_DoesNothing()
    {
        Editor editor = new("A");
        editor.Request();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Cancel);

        Assert.Equal(0, editor.SaveAllCount);
        Assert.Equal(0, editor.Proceeded);
        Assert.False(editor.Gate.IsQuestionPending);
    }

    [Fact]
    public void ARequestWhileAQuestionWaits_IsIgnored()
    {
        Editor editor = new("A");
        editor.Request();
        editor.Request();

        Assert.Single(editor.Asked);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);
        Assert.Equal(1, editor.Proceeded);
    }

    [Fact]
    public void ASecondAnswerToTheSameQuestion_IsIgnored()
    {
        Editor editor = new("A");
        editor.Request();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);
        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Equal(1, editor.Proceeded);
    }

    [Fact]
    public void NullArguments_FailEarly()
    {
        Assert.Throws<ArgumentNullException>(() => new ModifiedDocumentsGate(
            null, () => false, (_, _) => { }, () => true));
        Assert.Throws<ArgumentNullException>(() => new ModifiedDocumentsGate(
            () => Array.Empty<string>(), null, (_, _) => { }, () => true));
        Assert.Throws<ArgumentNullException>(() => new ModifiedDocumentsGate(
            () => Array.Empty<string>(), () => false, null, () => true));
        Assert.Throws<ArgumentNullException>(() => new ModifiedDocumentsGate(
            () => Array.Empty<string>(), () => false, (_, _) => { }, null));
        Assert.Throws<ArgumentNullException>(() => new Editor().Gate.Request(null));
    }
}
