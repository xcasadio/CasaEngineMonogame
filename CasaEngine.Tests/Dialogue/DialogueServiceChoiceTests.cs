using System.Collections.Generic;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

public sealed class DialogueServiceChoiceTests
{
    [Fact]
    public void ShowChoices_ExposesLabelsAndEntersAwaitingChoiceState()
    {
        var service = new DialogueService();
        service.TryOpen("Question?");

        bool shown = service.ShowChoices(new[] { "Yes", "No" });

        Assert.True(shown);
        Assert.True(service.IsOpen);
        Assert.Equal(DialogueRuntimeState.AwaitingChoice, service.State);
        Assert.True(service.HasChoices);
        Assert.Equal(new[] { "Yes", "No" }, service.Choices);
    }

    [Fact]
    public void SelectChoice_CompletesWithTheSelectedIndex()
    {
        var service = new DialogueService();
        service.TryOpen("Question?");
        service.ShowChoices(new[] { "Yes", "No" });

        DialogueChoiceSelectedEventArgs raised = null;
        service.ChoiceSelected += (_, args) => raised = args;

        bool selected = service.SelectChoice(1);

        Assert.True(selected);
        Assert.NotNull(raised);
        Assert.Equal(1, raised.SelectedIndex);
        Assert.Equal(new[] { "Yes", "No" }, raised.Labels);
        Assert.Equal("No", raised.Labels[raised.SelectedIndex]);
        Assert.False(service.HasChoices);
        Assert.Empty(service.Choices);
        Assert.Equal(DialogueRuntimeState.Open, service.State);
        Assert.True(service.IsOpen);
    }

    [Fact]
    public void SelectChoice_OutOfRange_Throws()
    {
        var service = new DialogueService();
        service.TryOpen("Question?");
        service.ShowChoices(new[] { "Yes", "No" });

        Assert.Throws<System.ArgumentOutOfRangeException>(() => service.SelectChoice(2));
    }

    [Fact]
    public void SelectChoice_WhenNotAwaitingChoice_ReturnsFalse()
    {
        var service = new DialogueService();
        service.TryOpen("Line only");

        bool selected = service.SelectChoice(0);

        Assert.False(selected);
    }

    [Fact]
    public void Close_ClearsChoiceState()
    {
        var service = new DialogueService();
        service.TryOpen("Question?");
        service.ShowChoices(new[] { "Yes", "No" });

        bool closed = service.Close();

        Assert.True(closed);
        Assert.False(service.IsOpen);
        Assert.False(service.HasChoices);
        Assert.Empty(service.Choices);
        Assert.Equal(DialogueRuntimeState.Closed, service.State);
    }

    [Fact]
    public void ShowChoices_AfterACompletedChoice_StartsFreshWithNoStaleState()
    {
        var service = new DialogueService();
        service.TryOpen("First question?");
        service.ShowChoices(new[] { "Yes", "No" });
        service.SelectChoice(0);

        bool shown = service.ShowChoices(new[] { "Left", "Middle", "Right" });

        Assert.True(shown);
        Assert.Equal(DialogueRuntimeState.AwaitingChoice, service.State);
        Assert.Equal(new[] { "Left", "Middle", "Right" }, service.Choices);
        Assert.DoesNotContain("Yes", service.Choices);
        Assert.DoesNotContain("No", service.Choices);
    }

    [Fact]
    public void ShowChoices_CalledAgainWhileAwaitingChoice_ReplacesThePreviousLabels()
    {
        var service = new DialogueService();
        service.TryOpen("Question?");
        service.ShowChoices(new[] { "Yes", "No" });

        // No SelectChoice in between: this isolates ShowChoices' own responsibility to
        // overwrite/clear its choice list, rather than relying on SelectChoice having already
        // cleared it (see ShowChoices_AfterACompletedChoice_StartsFreshWithNoStaleState below).
        bool shownAgain = service.ShowChoices(new[] { "Maybe" });

        Assert.True(shownAgain);
        Assert.Equal(new[] { "Maybe" }, service.Choices);
    }

    [Fact]
    public void ShowChoices_EmptyLabelList_Throws()
    {
        var service = new DialogueService();
        service.TryOpen("Question?");

        Assert.Throws<System.ArgumentException>(() => service.ShowChoices(System.Array.Empty<string>()));
    }

    /// <summary>
    /// Presenter-contract test: drives <see cref="DialogueService"/> only through the
    /// <see cref="IDialoguePresenter"/> interface (as a consumer/driver would) and records
    /// the order of state/choice notifications, proving ShowChoices → SelectChoice → Close
    /// are observed in that exact order by anything subscribing to the presenter contract.
    /// </summary>
    [Fact]
    public void PresenterContract_DrivenThroughInterface_NotifiesShowChoicesThenSelectionThenClose()
    {
        IDialoguePresenter presenter = new DialogueService();
        var recordedEvents = new List<string>();

        presenter.PresentationChanged += (_, args) => recordedEvents.Add($"Presentation:{args.CurrentState}");
        presenter.ChoiceSelected += (_, args) => recordedEvents.Add($"ChoiceSelected:{args.SelectedIndex}");

        presenter.ShowLine(new DialogueLine("Question?"));
        presenter.ShowChoices(new[] { "Yes", "No" });
        presenter.SelectChoice(1);
        presenter.Close();

        Assert.Equal(new[]
        {
            "Presentation:Open",
            "Presentation:AwaitingChoice",
            "Presentation:Open",
            "ChoiceSelected:1",
            "Presentation:Closed",
        }, recordedEvents);
    }
}
