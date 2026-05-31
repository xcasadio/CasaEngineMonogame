using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

public sealed class DialogueServiceTests
{
    [Fact]
    public void TryOpen_OpensDialogueWithCurrentLine()
    {
        var service = new DialogueService();

        bool opened = service.TryOpen("Bonjour depuis CasaEngine.");

        Assert.True(opened);
        Assert.True(service.IsOpen);
        Assert.Equal(DialogueRuntimeState.Open, service.State);
        Assert.Equal("Bonjour depuis CasaEngine.", service.CurrentLine.Text);
    }

    [Fact]
    public void Close_ClosesOpenDialogueAndClearsLine()
    {
        var service = new DialogueService();
        service.TryOpen("Line");

        bool closed = service.Close();

        Assert.True(closed);
        Assert.False(service.IsOpen);
        Assert.Equal(DialogueRuntimeState.Closed, service.State);
        Assert.True(service.CurrentLine.IsEmpty);
    }

    [Fact]
    public void TryOpen_ReturnsFalseWhenAlreadyOpen()
    {
        var service = new DialogueService();
        service.TryOpen("First");

        bool opened = service.TryOpen("Second");

        Assert.False(opened);
        Assert.True(service.IsOpen);
        Assert.Equal("First", service.CurrentLine.Text);
    }

    [Fact]
    public void Close_IsIdempotentWhenAlreadyClosed()
    {
        var service = new DialogueService();

        bool closed = service.Close();

        Assert.False(closed);
        Assert.False(service.IsOpen);
        Assert.True(service.CurrentLine.IsEmpty);
    }

    [Fact]
    public void StateChanged_RaisesOnlyForStateTransitions()
    {
        var service = new DialogueService();
        int eventCount = 0;
        DialogueStateChangedEventArgs lastArgs = null;
        service.StateChanged += (_, args) =>
        {
            eventCount++;
            lastArgs = args;
        };

        service.TryOpen("Line");
        service.TryOpen("Ignored");
        service.Close();
        service.Close();

        Assert.Equal(2, eventCount);
        Assert.NotNull(lastArgs);
        Assert.Equal(DialogueRuntimeState.Open, lastArgs.PreviousState);
        Assert.Equal(DialogueRuntimeState.Closed, lastArgs.CurrentState);
        Assert.True(lastArgs.CurrentLine.IsEmpty);
    }
}