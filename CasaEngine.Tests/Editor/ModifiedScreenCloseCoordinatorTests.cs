using System;
using System.Collections.Generic;
using CasaEngine.Editor.History;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// MGUI message boxes plan, T3.1 (D4, D5, ADR-0039): the asynchronous "save before closing?" question of screen documents.
/// A close that needs an answer is refused at once; the answer then saves and closes, closes without saving, or keeps the
/// screen - and only a close that happened retries a whole floating window's close.
/// </summary>
public class ModifiedScreenCloseCoordinatorTests
{
    private sealed class Editor
    {
        public HashSet<string> Modified { get; } = new();
        public HashSet<string> Open { get; } = new();
        public bool Automation { get; set; }
        public bool SaveSucceeds { get; set; } = true;
        public List<string> Asked { get; } = new();
        public List<Action<ModifiedScreenCloseDecision.Answer>> Answers { get; } = new();
        public List<string> Saved { get; } = new();
        public List<string> Closed { get; } = new();
        public int WindowRetries { get; set; }

        public ModifiedScreenCloseCoordinator Coordinator { get; }

        public Editor(params string[] openModifiedScreens)
        {
            foreach (string id in openModifiedScreens)
            {
                Open.Add(id);
                Modified.Add(id);
            }

            Coordinator = new ModifiedScreenCloseCoordinator(
                isModified: id => Modified.Contains(id),
                isAutomationActive: () => Automation,
                ask: (id, answered) =>
                {
                    Asked.Add(id);
                    Answers.Add(answered);
                },
                isScreenOpen: id => Open.Contains(id),
                trySave: id =>
                {
                    Saved.Add(id);
                    if (SaveSucceeds)
                    {
                        Modified.Remove(id);
                    }

                    return SaveSucceeds;
                });
        }

        /// <summary>What the docking host does on a user close: ask the coordinator, and close at once unless refused.</summary>
        public bool UserCloses(string id, Action retryWindowClose = null)
        {
            bool refused = Coordinator.OnClosing(id, () => CloseByCode(id), retryWindowClose);
            if (!refused)
            {
                Closed.Add(id);
                Open.Remove(id);
            }

            return refused;
        }

        public bool CloseByCode(string id)
        {
            if (!Open.Remove(id))
            {
                return false;
            }

            Closed.Add(id);
            return true;
        }

        public void Answer(int question, ModifiedScreenCloseDecision.Answer answer) => Answers[question](answer);
    }

    [Fact]
    public void AnUnmodifiedScreen_ClosesAtOnce_WithoutAQuestion()
    {
        Editor editor = new();
        editor.Open.Add("A");

        Assert.False(editor.UserCloses("A"));
        Assert.Empty(editor.Asked);
        Assert.Equal(new[] { "A" }, editor.Closed);
    }

    [Fact]
    public void UnderAutomation_AModifiedScreen_ClosesAtOnce_WithoutAQuestion()
    {
        Editor editor = new("A") { Automation = true };

        Assert.False(editor.UserCloses("A"));
        Assert.Empty(editor.Asked);
        Assert.Equal(new[] { "A" }, editor.Closed);
    }

    [Fact]
    public void AModifiedScreen_IsRefusedAtOnce_AndAskedOnce_EvenIfClosedAgainWhileTheQuestionWaits()
    {
        Editor editor = new("A");

        Assert.True(editor.UserCloses("A"));
        Assert.True(editor.UserCloses("A"));

        Assert.Equal(new[] { "A" }, editor.Asked);
        Assert.Empty(editor.Closed);
        Assert.True(editor.Coordinator.IsQuestionPending("A"));
    }

    [Fact]
    public void Save_SavesThenCloses_ThenRetriesTheWindowClose()
    {
        Editor editor = new("A");
        editor.UserCloses("A", () => editor.WindowRetries++);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Equal(new[] { "A" }, editor.Saved);
        Assert.Equal(new[] { "A" }, editor.Closed);
        Assert.Equal(1, editor.WindowRetries);
        Assert.False(editor.Coordinator.IsQuestionPending("A"));
    }

    [Fact]
    public void SaveThatFails_KeepsTheScreen_AndDoesNotRetry_AndAsksAgainOnTheNextClose()
    {
        Editor editor = new("A") { SaveSucceeds = false };
        editor.UserCloses("A", () => editor.WindowRetries++);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Equal(new[] { "A" }, editor.Saved);
        Assert.Empty(editor.Closed);
        Assert.Equal(0, editor.WindowRetries);
        Assert.Contains("A", editor.Open);

        Assert.True(editor.UserCloses("A"));
        Assert.Equal(new[] { "A", "A" }, editor.Asked);
    }

    [Fact]
    public void DontSave_ClosesWithoutSaving_ThenRetriesTheWindowClose()
    {
        Editor editor = new("A");
        editor.UserCloses("A", () => editor.WindowRetries++);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Empty(editor.Saved);
        Assert.Equal(new[] { "A" }, editor.Closed);
        Assert.Equal(1, editor.WindowRetries);
    }

    [Fact]
    public void Cancel_KeepsTheScreen_WithoutSaving_AndDoesNotRetry()
    {
        Editor editor = new("A");
        editor.UserCloses("A", () => editor.WindowRetries++);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Cancel);

        Assert.Empty(editor.Saved);
        Assert.Empty(editor.Closed);
        Assert.Equal(0, editor.WindowRetries);
        Assert.Contains("A", editor.Open);
        Assert.False(editor.Coordinator.IsQuestionPending("A"));
    }

    [Fact]
    public void AnAnswerForAScreenThatWentMeanwhile_DoesNothing()
    {
        Editor editor = new("A");
        editor.UserCloses("A", () => editor.WindowRetries++);
        editor.Open.Remove("A"); // closed by code, or the project was switched, while the question waited

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Empty(editor.Saved);
        Assert.Empty(editor.Closed);
        Assert.Equal(0, editor.WindowRetries);
    }

    [Fact]
    public void ACloseByCodeThatFails_DoesNotRetryTheWindowClose()
    {
        Editor editor = new("A");
        editor.Coordinator.OnClosing("A", () => false, () => editor.WindowRetries++);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Equal(0, editor.WindowRetries);
    }

    [Fact]
    public void ASecondAnswerToTheSameQuestion_IsIgnored()
    {
        Editor editor = new("A");
        editor.UserCloses("A");

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Cancel);
        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Empty(editor.Closed);
    }

    /// <summary>D4: "Close All" over three modified screens asks three questions; Cancel on the first keeps that screen
    /// only, and the other answers still apply.</summary>
    [Fact]
    public void CloseAll_AsksEveryModifiedScreen_AndCancelOnOneKeepsOnlyThatOne()
    {
        Editor editor = new("A", "B", "C");
        editor.Open.Add("Unmodified");

        foreach (string id in new[] { "A", "Unmodified", "B", "C" })
        {
            editor.UserCloses(id);
        }

        Assert.Equal(new[] { "A", "B", "C" }, editor.Asked);
        Assert.Equal(new[] { "Unmodified" }, editor.Closed);

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Cancel);
        editor.Answer(1, ModifiedScreenCloseDecision.Answer.Yes);
        editor.Answer(2, ModifiedScreenCloseDecision.Answer.No);

        Assert.Equal(new[] { "Unmodified", "B", "C" }, editor.Closed);
        Assert.Equal(new[] { "B" }, editor.Saved);
        Assert.Contains("A", editor.Open);
    }

    /// <summary>D5: a whole floating window with two modified screens. Its close stops at the first refusal; each answer
    /// that closes a screen retries the window close, which asks about the next one; Cancel stops the chain.</summary>
    [Theory]
    [InlineData(ModifiedScreenCloseDecision.Answer.Yes, ModifiedScreenCloseDecision.Answer.No, true)]
    [InlineData(ModifiedScreenCloseDecision.Answer.No, ModifiedScreenCloseDecision.Answer.Cancel, false)]
    [InlineData(ModifiedScreenCloseDecision.Answer.Cancel, ModifiedScreenCloseDecision.Answer.Yes, false)]
    public void AWholeFloatingWindow_IsRetriedAfterEachScreenItCloses(
        ModifiedScreenCloseDecision.Answer firstAnswer, ModifiedScreenCloseDecision.Answer secondAnswer, bool windowClosed)
    {
        Editor editor = new("A", "B");
        bool windowOpen = true;

        //  The docking host's whole-window close: asks each panel in order, stops (cancels) at the first refusal, and
        //  closes the window when no panel refuses.
        void CloseWindow()
        {
            foreach (string id in new[] { "A", "B" })
            {
                if (editor.Open.Contains(id) && editor.UserCloses(id, CloseWindow))
                {
                    return;
                }
            }

            windowOpen = false;
        }

        CloseWindow();
        Assert.Equal(new[] { "A" }, editor.Asked);

        editor.Answer(0, firstAnswer);
        if (editor.Answers.Count > 1)
        {
            editor.Answer(1, secondAnswer);
        }

        Assert.Equal(windowClosed, windowOpen == false);
        if (firstAnswer == ModifiedScreenCloseDecision.Answer.Cancel)
        {
            Assert.Equal(new[] { "A" }, editor.Asked); // Cancel stops the chain: B is not asked
            Assert.Contains("A", editor.Open);
            Assert.Contains("B", editor.Open);
        }
        else
        {
            Assert.Equal(new[] { "A", "B" }, editor.Asked);
        }
    }

    [Fact]
    public void NullArguments_FailEarly()
    {
        Assert.Throws<ArgumentNullException>(() => new ModifiedScreenCloseCoordinator(null, () => false, (_, _) => { }, _ => true, _ => true));
        Editor editor = new("A");
        Assert.Throws<ArgumentNullException>(() => editor.Coordinator.OnClosing(null, () => true, null));
        Assert.Throws<ArgumentNullException>(() => editor.Coordinator.OnClosing("A", null, null));
    }
}
