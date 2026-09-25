using System;
using CasaEngine.Editor.History;
using Xunit;

// Decide is obsolete since ADR-0039 (the editor asks asynchronously) but stays public; its cases are kept.
#pragma warning disable CS0618

namespace CasaEngine.Tests.Editor;

/// <summary>Bound screens program, phase 8, T4.5 (D17): every case of <see cref="ModifiedScreenCloseDecision.Decide"/>.</summary>
public class ModifiedScreenCloseDecisionTests
{
    private static Func<ModifiedScreenCloseDecision.Answer> AskThrows()
        => () => throw new InvalidOperationException("askUser must not be called.");

    private static Func<bool> SaveThrows()
        => () => throw new InvalidOperationException("trySave must not be called.");

    [Fact]
    public void NotModified_Proceeds_WithoutAskingOrSaving()
    {
        var result = ModifiedScreenCloseDecision.Decide(
            isModified: false,
            isAutomationActive: false,
            askUser: AskThrows(),
            trySave: SaveThrows());

        Assert.Equal(ModifiedScreenCloseDecision.Outcome.Proceed, result.Outcome);
        Assert.True(result.ShouldProceed);
        Assert.False(result.SaveAttempted);
    }

    [Fact]
    public void AutomationActive_Proceeds_WithoutAskingOrSaving_EvenIfModified()
    {
        var result = ModifiedScreenCloseDecision.Decide(
            isModified: true,
            isAutomationActive: true,
            askUser: AskThrows(),
            trySave: SaveThrows());

        Assert.True(result.ShouldProceed);
        Assert.False(result.SaveAttempted);
    }

    [Fact]
    public void Modified_AnswerYes_SaveSucceeds_Proceeds_AndAttemptedSave()
    {
        int askCount = 0, saveCount = 0;

        var result = ModifiedScreenCloseDecision.Decide(
            isModified: true,
            isAutomationActive: false,
            askUser: () => { askCount++; return ModifiedScreenCloseDecision.Answer.Yes; },
            trySave: () => { saveCount++; return true; });

        Assert.True(result.ShouldProceed);
        Assert.True(result.SaveAttempted);
        Assert.Equal(1, askCount);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void Modified_AnswerYes_SaveFails_Cancels_AndAttemptedSave()
    {
        int saveCount = 0;

        var result = ModifiedScreenCloseDecision.Decide(
            isModified: true,
            isAutomationActive: false,
            askUser: () => ModifiedScreenCloseDecision.Answer.Yes,
            trySave: () => { saveCount++; return false; });

        Assert.Equal(ModifiedScreenCloseDecision.Outcome.Cancel, result.Outcome);
        Assert.False(result.ShouldProceed);
        Assert.True(result.SaveAttempted);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void Modified_AnswerNo_Proceeds_WithoutSaving()
    {
        var result = ModifiedScreenCloseDecision.Decide(
            isModified: true,
            isAutomationActive: false,
            askUser: () => ModifiedScreenCloseDecision.Answer.No,
            trySave: SaveThrows());

        Assert.True(result.ShouldProceed);
        Assert.False(result.SaveAttempted);
    }

    [Fact]
    public void Modified_AnswerCancel_Cancels_WithoutSaving()
    {
        var result = ModifiedScreenCloseDecision.Decide(
            isModified: true,
            isAutomationActive: false,
            askUser: () => ModifiedScreenCloseDecision.Answer.Cancel,
            trySave: SaveThrows());

        Assert.Equal(ModifiedScreenCloseDecision.Outcome.Cancel, result.Outcome);
        Assert.False(result.ShouldProceed);
        Assert.False(result.SaveAttempted);
    }

    [Fact]
    public void AskUser_IsCalledExactlyOnce_ForEachAnswer()
    {
        foreach (var answer in new[]
                 {
                     ModifiedScreenCloseDecision.Answer.Yes,
                     ModifiedScreenCloseDecision.Answer.No,
                     ModifiedScreenCloseDecision.Answer.Cancel,
                 })
        {
            int askCount = 0;
            ModifiedScreenCloseDecision.Decide(
                isModified: true,
                isAutomationActive: false,
                askUser: () => { askCount++; return answer; },
                trySave: () => true);

            Assert.Equal(1, askCount);
        }
    }

    // ── ADR-0039: the two halves an asynchronous caller uses ─────────────────

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void NeedsAnswer_OnlyForAModifiedDocument_OutsideAutomation(bool isModified, bool isAutomationActive, bool expected)
        => Assert.Equal(expected, ModifiedScreenCloseDecision.NeedsAnswer(isModified, isAutomationActive));

    [Fact]
    public void ApplyAnswer_YesAndSaved_Proceeds_AfterSavingOnce()
    {
        int saveCount = 0;
        var result = ModifiedScreenCloseDecision.ApplyAnswer(ModifiedScreenCloseDecision.Answer.Yes, () => { saveCount++; return true; });

        Assert.True(result.ShouldProceed);
        Assert.True(result.SaveAttempted);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void ApplyAnswer_YesButSaveFails_Cancels()
    {
        var result = ModifiedScreenCloseDecision.ApplyAnswer(ModifiedScreenCloseDecision.Answer.Yes, () => false);

        Assert.False(result.ShouldProceed);
        Assert.True(result.SaveAttempted);
    }

    [Fact]
    public void ApplyAnswer_No_Proceeds_WithoutSaving()
    {
        var result = ModifiedScreenCloseDecision.ApplyAnswer(ModifiedScreenCloseDecision.Answer.No, SaveThrows());

        Assert.True(result.ShouldProceed);
        Assert.False(result.SaveAttempted);
    }

    [Fact]
    public void ApplyAnswer_Cancel_Cancels_WithoutSaving()
    {
        var result = ModifiedScreenCloseDecision.ApplyAnswer(ModifiedScreenCloseDecision.Answer.Cancel, SaveThrows());

        Assert.False(result.ShouldProceed);
        Assert.False(result.SaveAttempted);
    }

    [Fact]
    public void ApplyAnswer_Yes_WithoutASaveFunction_FailsEarly()
        => Assert.Throws<ArgumentNullException>(() => ModifiedScreenCloseDecision.ApplyAnswer(ModifiedScreenCloseDecision.Answer.Yes, null));
}
