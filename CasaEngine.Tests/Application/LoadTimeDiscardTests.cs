using CasaEngine.Framework.Application;
using Xunit;

namespace CasaEngine.Tests.Application;

/// <summary>
/// The time a world takes to start stays off the game clock, over its first two frames. <see cref="CasaEngineGame"/>
/// only forwards the loop to this class: no test builds that game, so what it wires is proved by a run of the Launcher.
/// </summary>
public class LoadTimeDiscardTests
{
    private int _resets;

    private LoadTimeDiscard NewDiscard() => new(() => _resets++);

    [Fact]
    public void WithNoWorldReady_NothingIsDropped()
    {
        var discard = NewDiscard();

        discard.UpdateStarting();
        discard.FramePresented();

        Assert.Equal(0, _resets);
    }

    [Fact]
    public void AWorldReady_DropsTheTimeOfItsLoadAtOnce()
    {
        var discard = NewDiscard();

        discard.WorldReady();

        Assert.Equal(1, _resets);
    }

    [Fact]
    public void BeforeTheFirstFrame_EveryUpdateRunsAtTimeZero()
    {
        var discard = NewDiscard();
        discard.WorldReady();

        discard.UpdateStarting();
        discard.UpdateStarting();

        Assert.Equal(3, _resets);
    }

    [Fact]
    public void TheFirstFramePresented_DropsWhatItCost()
    {
        var discard = NewDiscard();
        discard.WorldReady();
        discard.UpdateStarting();

        discard.FramePresented();

        Assert.Equal(3, _resets);
    }

    [Fact]
    public void TheSecondFrame_KeepsItsStep_ThenWhatItCostIsDropped()
    {
        var discard = NewDiscard();
        discard.WorldReady();
        discard.UpdateStarting();
        discard.FramePresented();

        discard.UpdateStarting();
        Assert.Equal(3, _resets);

        discard.FramePresented();
        Assert.Equal(4, _resets);
    }

    [Fact]
    public void AfterTheSecondFrame_TheTimeIsNormal()
    {
        var discard = NewDiscard();
        discard.WorldReady();
        discard.FramePresented();
        discard.FramePresented();
        int resets = _resets;

        discard.UpdateStarting();
        discard.FramePresented();

        Assert.Equal(resets, _resets);
    }

    [Fact]
    public void ALaterWorld_StartsTheDiscardAgain()
    {
        var discard = NewDiscard();
        discard.WorldReady();
        discard.FramePresented();
        discard.FramePresented();
        _resets = 0;

        discard.WorldReady();
        discard.UpdateStarting();

        Assert.Equal(2, _resets);
    }

    [Fact]
    public void AWorldReadyDuringTheSecondFrame_GoesBackToTimeZero()
    {
        var discard = NewDiscard();
        discard.WorldReady();
        discard.FramePresented();
        _resets = 0;

        discard.WorldReady();
        discard.UpdateStarting();

        Assert.Equal(2, _resets);
    }

    [Fact]
    public void TheRuntime_DiscardsWhenAWorldIsReady()
    {
        GameManager.DiscardLoadTime(GameplayExecutionPolicies.Runtime, NewDiscard());

        Assert.Equal(1, _resets);
    }

    /// <summary>The editor runs the loop: its game is never run, so nothing would ever end a discard started there.</summary>
    [Fact]
    public void TheEditor_DiscardsNothing()
    {
        GameManager.DiscardLoadTime(GameplayExecutionPolicies.EditorPreview, NewDiscard());
        GameManager.DiscardLoadTime(GameplayExecutionPolicies.EditorSimulation, NewDiscard());

        Assert.Equal(0, _resets);
    }
}
