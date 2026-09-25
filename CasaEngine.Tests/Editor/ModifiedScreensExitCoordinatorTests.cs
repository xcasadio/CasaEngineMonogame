using System;
using System.Collections.Generic;
using CasaEngine.Editor.History;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// MGUI message boxes plan, T3.2 (ADR-0041): the asynchronous "save before quitting?" question. An exit that needs an
/// answer is cancelled at once and asked about once; an answer that lets it go requests the exit again, and that exit - and
/// only that one - goes through without asking.
/// </summary>
public class ModifiedScreensExitCoordinatorTests
{
    private sealed class Editor
    {
        public List<string> Modified { get; } = new();
        public bool Automation { get; set; }
        public bool SaveSucceeds { get; set; } = true;
        public List<IReadOnlyList<string>> Logged { get; } = new();
        public List<IReadOnlyList<string>> Asked { get; } = new();
        public List<Action<ModifiedScreenCloseDecision.Answer>> Answers { get; } = new();
        public int SaveAllCount { get; private set; }
        public int ExitRequests { get; private set; }
        public int Exits { get; private set; }

        public ModifiedScreensExitCoordinator Coordinator { get; }

        public Editor(params string[] modified)
        {
            Modified.AddRange(modified);
            Coordinator = new ModifiedScreensExitCoordinator(
                modifiedScreenTitles: () => Modified.ToArray(),
                isAutomationActive: () => Automation,
                logAbandonedUnderAutomation: titles => Logged.Add(titles),
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
                },
                requestExit: () =>
                {
                    ExitRequests++;
                    UserQuits(); // Game.Exit raises OnExiting again
                });
        }

        /// <summary>What Game.OnExiting does: cancel when the coordinator says so, otherwise exit.</summary>
        public bool UserQuits()
        {
            bool cancelled = Coordinator.OnExiting();
            if (!cancelled)
            {
                Exits++;
            }

            return cancelled;
        }

        public void Answer(int question, ModifiedScreenCloseDecision.Answer answer) => Answers[question](answer);
    }

    [Fact]
    public void NothingModified_Exits_WithoutAQuestion()
    {
        Editor editor = new();

        Assert.False(editor.UserQuits());
        Assert.Empty(editor.Asked);
        Assert.Equal(1, editor.Exits);
    }

    [Fact]
    public void UnderAutomation_Exits_WithoutAQuestion_AndLogsTheAbandonedScreens()
    {
        Editor editor = new("A", "B") { Automation = true };

        Assert.False(editor.UserQuits());
        Assert.Empty(editor.Asked);
        Assert.Equal(new[] { "A", "B" }, Assert.Single(editor.Logged));
        Assert.Equal(1, editor.Exits);
    }

    [Fact]
    public void ModifiedScreens_CancelTheExit_AndAskOnce_EvenIfTheUserQuitsAgainWhileTheQuestionWaits()
    {
        Editor editor = new("A", "B");

        Assert.True(editor.UserQuits());
        Assert.True(editor.UserQuits());

        Assert.Equal(new[] { "A", "B" }, Assert.Single(editor.Asked));
        Assert.Equal(0, editor.Exits);
        Assert.True(editor.Coordinator.IsQuestionPending);
    }

    [Fact]
    public void Save_SavesEverything_ThenExits()
    {
        Editor editor = new("A", "B");
        editor.UserQuits();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Equal(1, editor.SaveAllCount);
        Assert.Equal(1, editor.ExitRequests);
        Assert.Equal(1, editor.Exits);
    }

    [Fact]
    public void SaveThatLeavesAScreenModified_StaysOpen_AndAsksAgainOnTheNextQuit()
    {
        Editor editor = new("A") { SaveSucceeds = false };
        editor.UserQuits();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Yes);

        Assert.Equal(1, editor.SaveAllCount);
        Assert.Equal(0, editor.ExitRequests);
        Assert.Equal(0, editor.Exits);

        Assert.True(editor.UserQuits());
        Assert.Equal(2, editor.Asked.Count);
    }

    [Fact]
    public void DontSave_Exits_WithoutSaving()
    {
        Editor editor = new("A");
        editor.UserQuits();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Equal(0, editor.SaveAllCount);
        Assert.Equal(1, editor.Exits);
    }

    [Fact]
    public void Cancel_StaysOpen_WithoutSaving_AndAsksAgainOnTheNextQuit()
    {
        Editor editor = new("A");
        editor.UserQuits();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Cancel);

        Assert.Equal(0, editor.SaveAllCount);
        Assert.Equal(0, editor.ExitRequests);
        Assert.Equal(0, editor.Exits);
        Assert.False(editor.Coordinator.IsQuestionPending);

        Assert.True(editor.UserQuits());
        Assert.Equal(2, editor.Asked.Count);
    }

    [Fact]
    public void TheExitLetThroughAfterAnAnswer_IsLetThroughOnce_TheNextOneAsksAgain()
    {
        Editor editor = new("A");
        editor.UserQuits();
        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No); // exit requested and let through
        Assert.Equal(1, editor.Exits);

        //  Had that exit not happened after all, a later quit with modified screens asks again.
        Assert.True(editor.UserQuits());
        Assert.Equal(2, editor.Asked.Count);
    }

    [Fact]
    public void ASecondAnswerToTheSameQuestion_IsIgnored()
    {
        Editor editor = new("A");
        editor.UserQuits();

        editor.Answer(0, ModifiedScreenCloseDecision.Answer.Cancel);
        editor.Answer(0, ModifiedScreenCloseDecision.Answer.No);

        Assert.Equal(0, editor.Exits);
    }

    [Fact]
    public void NullArguments_FailEarly()
    {
        Assert.Throws<ArgumentNullException>(() => new ModifiedScreensExitCoordinator(
            null, () => false, _ => { }, (_, _) => { }, () => true, () => { }));
        Assert.Throws<ArgumentNullException>(() => new ModifiedScreensExitCoordinator(
            () => Array.Empty<string>(), () => false, _ => { }, (_, _) => { }, () => true, null));
    }
}
