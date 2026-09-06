using System;
using System.Collections.Generic;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering.ScrollingLayers;

/// <summary>
/// <see cref="ScrollingLayerService"/>'s per-tick mechanism: V-animation cadence, auto-scroll
/// accumulation, the <see cref="ScrollingLayerService.SetFrame"/>/<see cref="ScrollingLayerService.Advance"/>
/// push/consume contract, the "last pushed" state, and <see cref="ScrollingLayerService.LayersVersion"/>
/// (plan plan-e9b-backdrops-moteur.md, D-E9b-1/D-E9b-3).
/// </summary>
public class ScrollingLayerServiceTests
{
    private static ScrollingLayerDefinition MakeLayer(int frameCount, int animTimer, int factorXNum = 1, int factorXDenom = 1,
        int factorYNum = 1, int factorYDenom = 1, int scrollXSpeed = 0, int scrollXPeriod = 0, int scrollYSpeed = 0, int scrollYPeriod = 0)
    {
        var frames = new Guid[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            frames[i] = Guid.NewGuid();
        }

        return new ScrollingLayerDefinition
        {
            FrameTextureAssetIds = frames,
            AnimTimer = animTimer,
            FactorXNum = factorXNum,
            FactorXDenom = factorXDenom,
            FactorYNum = factorYNum,
            FactorYDenom = factorYDenom,
            ScrollXSpeed = scrollXSpeed,
            ScrollXPeriod = scrollXPeriod,
            ScrollYSpeed = scrollYSpeed,
            ScrollYPeriod = scrollYPeriod,
            Pass = RenderPass2D.Background,
            Blend = SpriteBlendMode.Opaque,
            Tint = Color.White,
        };
    }

    private static ScrollingLayerService CreateService(ScrollingLayerDefinition layer)
    {
        var service = new ScrollingLayerService();
        service.SetConfiguration(new ScrollingLayerConfiguration(640, 480, 320, 240));
        service.SetLayers(new[] { layer });
        return service;
    }

    private static List<int> RepeatFrames(params (int frame, int count)[] plateaus)
    {
        var result = new List<int>();
        foreach (var (frame, count) in plateaus)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(frame);
            }
        }

        return result;
    }

    private static int AdvanceOneTickAndReadCounter(ScrollingLayerService service)
    {
        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance();
        Assert.True(service.TryGetLayerState(0, out var state));
        return state.AnimFrameCounter;
    }

    // ---- Cadence -----------------------------------------------------------------------------------

    [Fact]
    public void Advance_FourFramesTimer6_ProducesExactCadence_OverFortyTicks()
    {
        var service = CreateService(MakeLayer(frameCount: 4, animTimer: 6));

        var drawn = new List<int>();
        for (var tick = 0; tick < 40; tick++)
        {
            drawn.Add(AdvanceOneTickAndReadCounter(service));
        }

        var expected = RepeatFrames((0, 6), (1, 7), (2, 7), (3, 7), (0, 7), (1, 6));
        Assert.Equal(expected, drawn);
    }

    [Fact]
    public void Advance_FourFramesTimer4_FirstPlateauFour_ThenFivePerFrame()
    {
        var service = CreateService(MakeLayer(frameCount: 4, animTimer: 4));

        var drawn = new List<int>();
        for (var tick = 0; tick < 14; tick++)
        {
            drawn.Add(AdvanceOneTickAndReadCounter(service));
        }

        var expected = RepeatFrames((0, 4), (1, 5), (2, 5));
        Assert.Equal(expected, drawn);
    }

    [Fact]
    public void Advance_OneFrameLayer_CounterAlwaysZero()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 6));

        for (var tick = 0; tick < 40; tick++)
        {
            Assert.Equal(0, AdvanceOneTickAndReadCounter(service));
        }
    }

    // ---- Auto-scroll --------------------------------------------------------------------------------

    [Fact]
    public void Advance_SpeedZeroPeriodTen_AddsOnePixelAtTicksTenAndTwenty()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, scrollXSpeed: 0, scrollXPeriod: 10));

        int AdvanceAndReadOffset(int ticks)
        {
            service.SetFrame(0, 0, ticks, Vector3.Zero);
            service.Advance();
            Assert.True(service.TryGetLayerState(0, out var state));
            return state.AutoScrollOffsetX;
        }

        Assert.Equal(0, AdvanceAndReadOffset(9));   // ticks 1..9: no extra pixel yet.
        Assert.Equal(1, AdvanceAndReadOffset(1));   // tick 10: +1.
        Assert.Equal(1, AdvanceAndReadOffset(9));   // ticks 11..19: still 1.
        Assert.Equal(2, AdvanceAndReadOffset(1));   // tick 20: +1 again.
    }

    [Fact]
    public void Advance_SpeedTwoPeriodZero_AddsTwoPixelsPerTick()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, scrollXSpeed: 2, scrollXPeriod: 0));

        service.SetFrame(0, 0, 5, Vector3.Zero);
        service.Advance();

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(10, state.AutoScrollOffsetX);
    }

    [Theory]
    [InlineData(1, 10, 10, 11)]   // speed>=0, period>=0 -> dir +1: 1*10 + 1 = 11.
    [InlineData(-1, 10, 10, -11)] // speed<0, period>=0 -> dir -1: -1*10 - 1 = -11.
    [InlineData(1, -10, 10, 9)]   // speed>=0, period<0 -> dir -1: 1*10 - 1 = 9.
    [InlineData(-1, -10, 10, -9)] // speed<0, period<0 -> dir +1: -1*10 + 1 = -9.
    public void Advance_AutoScrollDirection_FollowsSpeedXorPeriodSign(int speed, int period, int ticks, int expectedOffset)
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, scrollXSpeed: speed, scrollXPeriod: period));

        service.SetFrame(0, 0, ticks, Vector3.Zero);
        service.Advance();

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(expectedOffset, state.AutoScrollOffsetX);
    }

    // ---- ticks = 0 / ticks = 4 ------------------------------------------------------------------------

    [Fact]
    public void Advance_WithZeroTicks_AdvancesNothing()
    {
        var service = CreateService(MakeLayer(frameCount: 4, animTimer: 0, scrollXSpeed: 1));

        service.SetFrame(0, 0, 0, Vector3.Zero);
        service.Advance();

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(0, state.AnimFrameCounter);
        Assert.Equal(0, state.AutoScrollOffsetX);
    }

    [Fact]
    public void Advance_WithFourTicks_AdvancesExactlyFourTimes()
    {
        var service = CreateService(MakeLayer(frameCount: 4, animTimer: 0, scrollXSpeed: 1));

        service.SetFrame(0, 0, 4, Vector3.Zero);
        service.Advance();

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(0, state.AnimFrameCounter); // AnimTimer 0: advances every tick, 4 mod 4 = 0.
        Assert.Equal(4, state.AutoScrollOffsetX);
    }

    // ---- Consumption ----------------------------------------------------------------------------------

    [Fact]
    public void SetFrame_ArmsAPendingFrame_ConsumedOnceByAdvance()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100));

        Assert.False(service.HasPendingFrame);

        service.SetFrame(0, 0, 2, Vector3.Zero);
        Assert.True(service.HasPendingFrame);
        Assert.Equal(2, service.PendingTicks);

        service.Advance();
        Assert.False(service.HasPendingFrame);
        Assert.Equal(0, service.PendingTicks);
    }

    [Fact]
    public void SetFrameTwoTicks_ThenTwoAdvanceCalls_AdvancesExactlyTwoTicksNotFour()
    {
        var service = CreateService(MakeLayer(frameCount: 4, animTimer: 0, scrollXSpeed: 1));

        service.SetFrame(0, 0, 2, Vector3.Zero);
        service.Advance();
        service.Advance(); // no new SetFrame in between: PendingTicks is already 0.

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(2, state.AutoScrollOffsetX); // 2 ticks of +1, not 4.
        Assert.Equal(2, state.AnimFrameCounter);
    }

    [Fact]
    public void SecondSetFrame_WithoutAnInterveningAdvance_OverwritesThePendingFrame()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100));

        service.SetFrame(10, 20, 5, new Vector3(1f, 2f, 0f));
        service.SetFrame(30, 40, 1, new Vector3(3f, 4f, 0f));

        Assert.Equal(1, service.PendingTicks);
        Assert.Equal(30, service.LastPushedScrollX);
        Assert.Equal(40, service.LastPushedScrollY);
        Assert.Equal(new Vector3(3f, 4f, 0f), service.CameraTarget);
    }

    // ---- Last-pushed state ------------------------------------------------------------------------------

    [Fact]
    public void SetFrame_UpdatesLastPushedStateAndFramesPushed_WithoutRequiringAdvance()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100));

        Assert.Equal(0, service.FramesPushed);

        service.SetFrame(927, 719, 3, new Vector3(1087f, -839f, 0f));

        Assert.Equal(1, service.FramesPushed);
        Assert.Equal(927, service.LastPushedScrollX);
        Assert.Equal(719, service.LastPushedScrollY);
        Assert.Equal(new Vector3(1087f, -839f, 0f), service.CameraTarget);

        service.SetFrame(0, 0, 0, Vector3.Zero);
        Assert.Equal(2, service.FramesPushed);
    }

    [Fact]
    public void Clear_ResetsFramesPushedAndThePendingFrame()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100));
        service.SetFrame(1, 1, 1, Vector3.One);

        service.Clear();

        Assert.Equal(0, service.FramesPushed);
        Assert.False(service.HasPendingFrame);
        Assert.Equal(0, service.PendingTicks);
        Assert.Equal(0, service.LayerCount);
    }

    // ---- Offset pins (D-E9b-11) --------------------------------------------------------------------------

    [Fact]
    public void Advance_FactorZeroOverOne_GivesZeroOffsetRegardlessOfScroll()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, factorXNum: 0, factorXDenom: 1, factorYNum: 0, factorYDenom: 1));

        service.SetFrame(927, 719, 0, new Vector3(1087f, -839f, 0f));
        service.Advance();

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(0, state.LayerOffsetX);
        Assert.Equal(0, state.LayerOffsetY);
    }

    [Fact]
    public void Advance_FactorOneOverOne_GivesTheWrappedScroll_287And239()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, factorXNum: 1, factorXDenom: 1, factorYNum: 1, factorYDenom: 1));

        service.SetFrame(927, 719, 0, new Vector3(1087f, -839f, 0f));
        service.Advance();

        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(287, state.LayerOffsetX);
        Assert.Equal(239, state.LayerOffsetY);
    }

    [Fact]
    public void Advance_RecomputesOffsetEvenWithZeroTicks_SoANewPushShowsUpImmediately()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, factorXNum: 1, factorXDenom: 1));

        service.SetFrame(100, 0, 0, Vector3.Zero);
        service.Advance();
        Assert.True(service.TryGetLayerState(0, out var first));
        Assert.Equal(100, first.LayerOffsetX);

        service.SetFrame(200, 0, 0, Vector3.Zero);
        service.Advance();
        Assert.True(service.TryGetLayerState(0, out var second));
        Assert.Equal(200, second.LayerOffsetX);
    }

    // ---- LayersVersion --------------------------------------------------------------------------------

    [Fact]
    public void LayersVersion_StartsAtZero_AndStrictlyIncreasesOnSetLayersSetTintAndClear()
    {
        var service = new ScrollingLayerService();
        Assert.Equal(0, service.LayersVersion);

        service.SetLayers(new[] { MakeLayer(1, 0) });
        Assert.Equal(1, service.LayersVersion);

        service.SetTint(new ScrollingTintDefinition(Color.White, new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0)));
        Assert.Equal(2, service.LayersVersion);

        service.Clear();
        Assert.Equal(3, service.LayersVersion); // never reset to 0.

        service.SetLayers(new[] { MakeLayer(1, 0) });
        Assert.Equal(4, service.LayersVersion);
    }

    [Fact]
    public void TwoClearSetLayersCycles_ProduceTwoDistinctVersions_AndFreshlyResetCounters()
    {
        var service = new ScrollingLayerService();
        service.SetConfiguration(new ScrollingLayerConfiguration(640, 480, 320, 240));

        service.SetLayers(new[] { MakeLayer(4, 0) });
        var versionAfterFirst = service.LayersVersion;
        AdvanceOneTickAndReadCounter(service);
        AdvanceOneTickAndReadCounter(service);
        Assert.True(service.TryGetLayerState(0, out var midCycleState));
        Assert.Equal(2, midCycleState.AnimFrameCounter);

        service.Clear();
        service.SetLayers(new[] { MakeLayer(4, 0) });
        var versionAfterSecond = service.LayersVersion;

        Assert.NotEqual(versionAfterFirst, versionAfterSecond);
        Assert.True(service.TryGetLayerState(0, out var freshState));
        Assert.Equal(0, freshState.AnimFrameCounter); // SetLayers rebuilds runtime state from scratch.
    }

    // ---- ResetLayerRuntimeState (D-E9b-7, S0 fix item 2) -----------------------------------------------

    [Fact]
    public void BareSetTint_AloneDoesNotResetCounters_OnlyAnExplicitResetDoes()
    {
        var service = CreateService(MakeLayer(frameCount: 1, animTimer: 100, scrollXSpeed: 1));

        service.SetFrame(0, 0, 5, Vector3.Zero);
        service.Advance();
        Assert.True(service.TryGetLayerState(0, out var beforeTint));
        Assert.Equal(5, beforeTint.AutoScrollOffsetX); // 5 ticks accumulated.

        // A bare SetTint (no SetLayers) bumps LayersVersion but must not, by itself, reset anything -
        // that is the component's explicit job once it observes the version change (ResolveTextures).
        service.SetTint(new ScrollingTintDefinition(Color.White, new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0)));
        Assert.True(service.TryGetLayerState(0, out var afterTint));
        Assert.Equal(5, afterTint.AutoScrollOffsetX); // untouched by SetTint alone.

        service.ResetLayerRuntimeState();
        Assert.True(service.TryGetLayerState(0, out var afterReset));
        Assert.Equal(0, afterReset.AutoScrollOffsetX);
        Assert.Equal(0, afterReset.AnimFrameCounter);
        Assert.Equal(0, afterReset.LayerOffsetX);
    }

    [Fact]
    public void ResetLayerRuntimeState_KeepsLayerDefinitionAndLayersVersionAndTint()
    {
        var layer = MakeLayer(frameCount: 3, animTimer: 0, scrollXSpeed: 2);
        var service = CreateService(layer);
        var tint = new ScrollingTintDefinition(Color.Red, new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0));
        service.SetTint(tint);

        service.SetFrame(0, 0, 3, Vector3.Zero);
        service.Advance();
        var versionBefore = service.LayersVersion;

        service.ResetLayerRuntimeState();

        Assert.Equal(versionBefore, service.LayersVersion); // reset never touches LayersVersion.
        Assert.True(service.Tint.HasValue);
        Assert.Equal(tint, service.Tint.Value); // nor the tint.
        Assert.Equal(layer.FrameTextureAssetIds, service.GetLayerDefinition(0).FrameTextureAssetIds); // nor the definition.
        Assert.True(service.TryGetLayerState(0, out var state));
        Assert.Equal(0, state.AnimFrameCounter);
        Assert.Equal(0, state.AnimFrameTimer);
        Assert.Equal(0, state.AutoScrollOffsetX);
        Assert.Equal(0, state.AutoScrollOffsetY);
        Assert.Equal(0, state.TimerX);
        Assert.Equal(0, state.TimerY);
        Assert.Equal(0, state.LayerOffsetX);
        Assert.Equal(0, state.LayerOffsetY);
    }
}
