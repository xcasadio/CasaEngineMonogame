using CasaEngine.Framework.Application;
using CasaEngine.Framework.Configuration.Project;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Application;

/// <summary>
/// The virtual resolution in force for the loaded world (ADR-0086): a world without a block keeps the project's
/// setting, a world block keeps its size and mode with the project's <c>ScaleUI</c> (false without a project setting),
/// a world change replaces it, and the editor has none.
/// </summary>
public class WorldVirtualResolutionStateTests
{
    private static readonly VirtualResolutionSettings ScaledProject = new() { Width = 320, Height = 180, ScaleUI = true };

    [Fact]
    public void AWorldWithoutABlock_KeepsTheProjectSetting()
    {
        var state = new WorldVirtualResolutionState();

        state.UseWorld(ScaledProject, null);

        Assert.Same(ScaledProject, state.Active(ScaledProject, externalViewManagement: false));
    }

    [Theory]
    [InlineData(VirtualResolutionMode.IntegerFit)]
    [InlineData(VirtualResolutionMode.BufferedIntegerFit)]
    [InlineData(VirtualResolutionMode.BufferedFit)]
    public void AWorldBlock_WinsWithTheProjectScaleUI(VirtualResolutionMode mode)
    {
        var state = new WorldVirtualResolutionState();

        state.UseWorld(ScaledProject, new VirtualResolutionSettings { Width = 640, Height = 480, Mode = mode });
        var active = state.Active(ScaledProject, externalViewManagement: false);

        Assert.Equal(640, active.Width);
        Assert.Equal(480, active.Height);
        Assert.Equal(mode, active.Mode);
        Assert.True(active.ScaleUI);
    }

    [Fact]
    public void AnIntegerFitWorldBlock_InAScaledProject_GivesScreensTheWorldSize()
    {
        var composed = WorldVirtualResolutionState.Compose(
            ScaledProject, new VirtualResolutionSettings { Width = 640, Height = 480, Mode = VirtualResolutionMode.IntegerFit });

        Assert.Equal(new Point(640, 480), GameManager.DesignResolutionOf(composed));
    }

    [Fact]
    public void AWorldBlock_InAProjectWithoutASetting_HasNoScaleUI_AndNoDesignResolution()
    {
        var composed = WorldVirtualResolutionState.Compose(null, new VirtualResolutionSettings { Width = 640, Height = 480 });

        Assert.False(composed.ScaleUI);
        Assert.Null(GameManager.DesignResolutionOf(composed));
    }

    [Fact]
    public void AWorldChange_ReplacesTheResolution()
    {
        var state = new WorldVirtualResolutionState();

        state.UseWorld(ScaledProject, new VirtualResolutionSettings { Width = 640, Height = 480, Mode = VirtualResolutionMode.BufferedFit });
        Assert.Equal(640, state.Active(ScaledProject, externalViewManagement: false).Width);

        state.UseWorld(ScaledProject, new VirtualResolutionSettings { Width = 320, Height = 240, Mode = VirtualResolutionMode.BufferedIntegerFit });
        Assert.Equal(320, state.Active(ScaledProject, externalViewManagement: false).Width);

        state.UseWorld(ScaledProject, null);
        Assert.Same(ScaledProject, state.Active(ScaledProject, externalViewManagement: false));
    }

    [Fact]
    public void TheEditor_HasNoActiveResolution_EvenWithAWorldBlock()
    {
        var state = new WorldVirtualResolutionState();

        state.UseWorld(ScaledProject, new VirtualResolutionSettings { Width = 640, Height = 480 });

        Assert.Null(state.Active(ScaledProject, externalViewManagement: true));
    }

    [Fact]
    public void ReadingTheActiveResolution_ReturnsTheSameInstanceEveryTime()
    {
        var state = new WorldVirtualResolutionState();
        state.UseWorld(ScaledProject, new VirtualResolutionSettings { Width = 640, Height = 480 });

        Assert.Same(state.Active(ScaledProject, false), state.Active(ScaledProject, false));
    }
}
