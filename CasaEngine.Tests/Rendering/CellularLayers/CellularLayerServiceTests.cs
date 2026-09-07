using System;
using CasaEngine.Framework.Rendering.CellularLayers;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Rendering.CellularLayers;

/// <summary>
/// <see cref="CellularLayerService"/>'s per-tick mechanism: the cadence shared by every cell type
/// (<c>AnimFrameTimer</c>/<c>AnimFrameCounter</c>/<c>WaveTick</c>, including the byte wrap), each
/// <see cref="CellularCellType"/>'s own drift/wrap/respawn behaviour, and the
/// <see cref="CellularLayerService.SetFrame"/>/<see cref="CellularLayerService.Advance"/> push/consume
/// contract (docs/plan-e9d-mode-cellulaire.md D3).
/// </summary>
public class CellularLayerServiceTests
{
    private static CellularCellDefinition MakeCell(
        CellularCellType type, int x0 = 0, int y0 = 0,
        int u0 = 0, int u1 = 15, int v0 = 0, int v1 = 15,
        int dx = 0, int periodX = 0, int dy = 0, int periodY = 0,
        int camXNum = 0, int camXDen = 1, int camYNum = 0, int camYDen = 1)
    {
        return new CellularCellDefinition
        {
            Type = type,
            X0 = x0,
            Y0 = y0,
            U0 = u0,
            U1 = u1,
            V0 = v0,
            V1 = v1,
            DX = dx,
            PeriodX = periodX,
            DY = dy,
            PeriodY = periodY,
            CamXNum = camXNum,
            CamXDen = camXDen,
            CamYNum = camYNum,
            CamYDen = camYDen,
        };
    }

    private static CellularLayerService CreateService(params CellularCellDefinition[] cells)
    {
        var service = new CellularLayerService();
        service.SetLayers(new[]
        {
            new CellularLayerDefinition
            {
                AnimTimer = 100,
                AnimNum = 1,
                Cells = cells,
            },
        });
        return service;
    }

    private static uint ZeroRandom() => 0u;

    // ---- Cadence: AnimFrameTimer / AnimFrameCounter --------------------------------------------------

    [Fact]
    public void Advance_FourPhasesTimer6_ProducesExactCadence_OverFortyTicks()
    {
        var service = new CellularLayerService();
        service.SetLayers(new[]
        {
            new CellularLayerDefinition { AnimTimer = 6, AnimNum = 4, Cells = Array.Empty<CellularCellDefinition>() },
        });

        var drawn = new System.Collections.Generic.List<int>();
        for (var tick = 0; tick < 40; tick++)
        {
            service.SetFrame(0, 0, 1, Vector3.Zero);
            service.Advance(ZeroRandom);
            Assert.True(service.TryGetLayerState(0, out var state));
            drawn.Add(state.AnimFrameCounter);
        }

        // Same shape as the sibling mechanism's own cadence port: plateau of AnimTimer+1 ticks on
        // phase 0 (first phase never got the "already advanced once" head start), AnimTimer+2 on the
        // ones after.
        var expected = new System.Collections.Generic.List<int>();
        void Repeat(int phase, int count) { for (var i = 0; i < count; i++) expected.Add(phase); }
        Repeat(0, 6); Repeat(1, 7); Repeat(2, 7); Repeat(3, 7); Repeat(0, 7); Repeat(1, 6);
        Assert.Equal(expected, drawn);
    }

    [Fact]
    public void Advance_WaveTick_WrapsModulo256()
    {
        var service = new CellularLayerService();
        service.SetLayers(new[]
        {
            new CellularLayerDefinition { AnimTimer = 100, AnimNum = 1, Cells = Array.Empty<CellularCellDefinition>() },
        });

        service.SetFrame(0, 0, 256, Vector3.Zero);
        service.Advance(ZeroRandom);
        Assert.True(service.TryGetLayerState(0, out var stateAt256));
        Assert.Equal((byte)0, stateAt256.WaveTick); // 256 ticks -> wraps exactly back to 0.

        service.SetFrame(0, 0, 4, Vector3.Zero); // 256 + 4 = 260 ticks total.
        service.Advance(ZeroRandom);
        Assert.True(service.TryGetLayerState(0, out var stateAt260));
        Assert.Equal((byte)4, stateAt260.WaveTick);
    }

    // ---- Normal: OR-of-signs period step, integration-level ------------------------------------------

    [Fact]
    public void Advance_NormalCell_BothDxAndPeriodXNegative_UsesOrOfSigns_NotTheSiblingsXor()
    {
        // DX=-1, PeriodX=-10: OR gives step -1 (both negative). The sibling mechanism's own XOR rule
        // would give +1 here instead - this is the one case where the two diverge (§1.4).
        // Wide U0/U1 keeps the wrap out of the way so only the drift/period math is exercised.
        var cell = MakeCell(CellularCellType.Normal, x0: 100, u0: 0, u1: 1000, dx: -1, periodX: -10);
        var service = CreateService(cell);

        service.SetFrame(0, 0, 10, Vector3.Zero);
        service.Advance(ZeroRandom);

        // 10 ticks of DX=-1 (-10) plus one extra period step at tick 10 (OR => -1): 100 - 10 - 1 = 89.
        // Had the sibling's XOR rule been used instead, the extra step would be +1: 100 - 10 + 1 = 91.
        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.True(state.ShouldDraw);
        Assert.Equal(89, state.DrawX);
    }

    // ---- Normal: wrap on both axes, both bounds -------------------------------------------------------

    [Fact]
    public void Advance_NormalCell_WrapsAtTheLowXBound()
    {
        // minX = U0 - U1 = 0 - 15 = -15. X0 = -20 is below it.
        var cell = MakeCell(CellularCellType.Normal, x0: -20, y0: 0, u0: 0, u1: 15, v0: 0, v1: 15);
        var service = CreateService(cell);

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(ZeroRandom);

        // posX += 320 - minX = 320 + 15 = 335 -> posX = -20 + 335 = 315.
        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.Equal(315, state.DrawX);
        Assert.Equal(0, state.DrawY);
    }

    [Fact]
    public void Advance_NormalCell_WrapsAtTheHighXBound()
    {
        // minX = -15. X0 = 400 is above ScreenWidth - 1 = 319.
        var cell = MakeCell(CellularCellType.Normal, x0: 400, y0: 0, u0: 0, u1: 15, v0: 0, v1: 15);
        var service = CreateService(cell);

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(ZeroRandom);

        // posX += -320 + minX = -320 - 15 = -335 -> posX = 400 - 335 = 65.
        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.Equal(65, state.DrawX);
    }

    [Fact]
    public void Advance_NormalCell_WrapsAtTheLowYBound()
    {
        // minY = V0 - V1 = -15. Y0 = -20 is below it.
        var cell = MakeCell(CellularCellType.Normal, x0: 0, y0: -20, u0: 0, u1: 15, v0: 0, v1: 15);
        var service = CreateService(cell);

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(ZeroRandom);

        // posY += 240 - minY = 240 + 15 = 255 -> posY = -20 + 255 = 235.
        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.Equal(0, state.DrawX);
        Assert.Equal(235, state.DrawY);
    }

    [Fact]
    public void Advance_NormalCell_WrapsAtTheHighYBound()
    {
        // minY = -15. Y0 = 400 is above ScreenHeight - 1 = 239.
        var cell = MakeCell(CellularCellType.Normal, x0: 0, y0: 400, u0: 0, u1: 15, v0: 0, v1: 15);
        var service = CreateService(cell);

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(ZeroRandom);

        // posY += -240 + minY = -240 - 15 = -255 -> posY = 400 - 255 = 145.
        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.Equal(145, state.DrawY);
    }

    // ---- FallRespawn: no Y wrap, respawns at a random top X instead -----------------------------------

    [Fact]
    public void Advance_FallRespawnCell_PastTheBottom_RespawnsAtTheTop_WithAnAbscissaWithinScreenBounds()
    {
        // DY=300 overshoots ScreenHeight-1=239 in a single tick; the invariants pinned here (D7) are
        // "reappears near the top" and "abscissa within screen bounds" - never the absolute position,
        // since FallRespawn deliberately shares the game's own global random stream.
        var cell = MakeCell(CellularCellType.FallRespawn, x0: 0, y0: 0, u0: 0, u1: 15, v0: 0, v1: 15, dy: 300);
        var service = CreateService(cell);
        var randomCallCount = 0;
        uint Random()
        {
            randomCallCount++;
            return 0x80000000u; // exactly mid-range: ((ulong)0x80000000 * 320) >> 32 == 160.
        }

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(Random);

        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.True(state.ShouldDraw);
        Assert.InRange(state.DrawX, 0, CellularLayerService.ScreenWidth - 1); // invariant: on screen.
        Assert.True(state.DrawY < CellularLayerService.ScreenHeight); // invariant: reappeared, not still falling.
        Assert.Equal(1, randomCallCount); // called exactly once, only because this cell actually respawned.
    }

    [Fact]
    public void Advance_FallRespawnCell_BeforeTheBottom_NeverCallsTheRandomSource()
    {
        var cell = MakeCell(CellularCellType.FallRespawn, x0: 0, y0: 0, u0: 0, u1: 15, v0: 0, v1: 15, dy: 1);
        var service = CreateService(cell);
        var randomCallCount = 0;
        uint Random() { randomCallCount++; return 0; }

        service.SetFrame(0, 0, 5, Vector3.Zero); // posY reaches 5, nowhere near the 239 bound.
        service.Advance(Random);

        Assert.Equal(0, randomCallCount);
    }

    // ---- ScriptTrack: literal no-op -------------------------------------------------------------------

    [Fact]
    public void Advance_ScriptTrackCell_NeverDrawsAndAdvancesNoState()
    {
        var cell = MakeCell(CellularCellType.ScriptTrack, x0: 42, y0: 7, dx: 5, dy: 5, periodX: 1, periodY: 1);
        var service = CreateService(cell);

        for (var tick = 0; tick < 10; tick++)
        {
            service.SetFrame(0, 0, 1, Vector3.Zero);
            service.Advance(ZeroRandom);

            Assert.True(service.TryGetCellState(0, 0, out var state));
            Assert.False(state.ShouldDraw);
            Assert.Equal(0, state.DrawX); // never touched - stays at the CellRuntime default, not X0/DX.
            Assert.Equal(0, state.DrawY);
        }
    }

    // ---- WaveX: the empty-WaveLut guard skips THIS cell only, the loop goes on ----------------------

    [Fact]
    public void Advance_WaveXCell_WithEmptyWaveLut_SkipsOnlyItself_AndLaterCellsStillAdvance()
    {
        // The original's guard is `break` inside a `switch` case (GraphicManager.cs:1190-1193): it
        // leaves the switch, not the cell loop. A layer mixing WaveX with other types therefore still
        // advances and draws those when the lookup table is absent.
        var waveCell = MakeCell(CellularCellType.WaveX, x0: 10, y0: 10);
        var normalAfter = MakeCell(CellularCellType.Normal, x0: 50, y0: 50, u0: 0, u1: 1000, v0: 0, v1: 1000);
        var service = CreateService(waveCell, normalAfter);
        // No SetWaveLut call - WaveLut starts empty.

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(ZeroRandom);

        Assert.True(service.TryGetCellState(0, 0, out var waveState));
        Assert.False(waveState.ShouldDraw); // the formula was never attempted for this one.

        Assert.True(service.TryGetCellState(0, 1, out var normalState));
        Assert.True(normalState.ShouldDraw); // and the Normal cell after it ran normally.
        Assert.Equal(50, normalState.DrawX);
        Assert.Equal(50, normalState.DrawY);
    }

    [Fact]
    public void Advance_WaveXCell_WithNonEmptyWaveLut_Draws()
    {
        var waveCell = MakeCell(CellularCellType.WaveX, x0: 10, y0: 10);
        var service = CreateService(waveCell);
        service.SetWaveLut(new int[256]); // all zero, but non-empty: the loop is not skipped.

        service.SetFrame(0, 0, 1, Vector3.Zero);
        service.Advance(ZeroRandom);

        Assert.True(service.TryGetCellState(0, 0, out var state));
        Assert.True(state.ShouldDraw);
        Assert.Equal(10, state.DrawY); // WaveX never moves in Y.
    }

    // ---- Push/consume contract (mirrors the sibling mechanism's own) ---------------------------------

    [Fact]
    public void SetFrame_ArmsAPendingFrame_ConsumedOnceByAdvance()
    {
        var service = CreateService(MakeCell(CellularCellType.Normal));

        Assert.False(service.HasPendingFrame);

        service.SetFrame(0, 0, 2, Vector3.Zero);
        Assert.True(service.HasPendingFrame);
        Assert.Equal(2, service.PendingTicks);

        service.Advance(ZeroRandom);
        Assert.False(service.HasPendingFrame);
        Assert.Equal(0, service.PendingTicks);
    }

    [Fact]
    public void SecondSetFrame_WithoutAnInterveningAdvance_OverwritesThePendingFrame()
    {
        var service = CreateService(MakeCell(CellularCellType.Normal));

        service.SetFrame(10, 20, 5, new Vector3(1f, 2f, 0f));
        service.SetFrame(30, 40, 1, new Vector3(3f, 4f, 0f));

        Assert.Equal(1, service.PendingTicks);
        Assert.Equal(30, service.LastPushedCameraX);
        Assert.Equal(40, service.LastPushedCameraY);
        Assert.Equal(new Vector3(3f, 4f, 0f), service.CameraTarget);
    }

    [Fact]
    public void Clear_ResetsFramesPushedAndThePendingFrame_AndTheWaveLut()
    {
        var service = CreateService(MakeCell(CellularCellType.Normal));
        service.SetWaveLut(new int[] { 1, 2, 3 });
        service.SetFrame(1, 1, 1, Vector3.One);

        service.Clear();

        Assert.Equal(0, service.FramesPushed);
        Assert.False(service.HasPendingFrame);
        Assert.Equal(0, service.PendingTicks);
        Assert.Equal(0, service.LayerCount);
        Assert.Empty(service.WaveLut.ToArray());
    }

    [Fact]
    public void LayersVersion_StartsAtZero_AndStrictlyIncreasesOnSetLayersAndClear()
    {
        var service = new CellularLayerService();
        Assert.Equal(0, service.LayersVersion);

        service.SetLayers(new[] { new CellularLayerDefinition { AnimNum = 1, Cells = Array.Empty<CellularCellDefinition>() } });
        Assert.Equal(1, service.LayersVersion);

        service.Clear();
        Assert.Equal(2, service.LayersVersion); // never reset to 0.

        service.SetLayers(new[] { new CellularLayerDefinition { AnimNum = 1, Cells = Array.Empty<CellularCellDefinition>() } });
        Assert.Equal(3, service.LayersVersion);
    }

    [Fact]
    public void ResetLayerRuntimeState_ReseedsEachCellAtItsOwnX0Y0_AndZeroesLayerCadence()
    {
        var cell = MakeCell(CellularCellType.Normal, x0: 42, y0: 7, u0: 0, u1: 1000, v0: 0, v1: 1000, dx: 1, dy: 1);
        var service = CreateService(cell);

        service.SetFrame(0, 0, 5, Vector3.Zero);
        service.Advance(ZeroRandom);
        Assert.True(service.TryGetCellState(0, 0, out var beforeReset));
        Assert.NotEqual(42, beforeReset.DrawX); // drifted away from the seed.

        service.ResetLayerRuntimeState();

        Assert.True(service.TryGetLayerState(0, out var layerState));
        Assert.Equal(0, layerState.AnimFrameTimer);
        Assert.Equal((byte)0, layerState.WaveTick);
        // The cell has not been re-advanced yet, so ShouldDraw/DrawX/DrawY are back to the runtime
        // struct's own defaults - only a fresh Advance() re-populates them, seeded from X0/Y0.
        service.SetFrame(0, 0, 0, Vector3.Zero);
        service.Advance(ZeroRandom); // zero ticks: cells never run, so this only clears HasPendingFrame.
        Assert.True(service.TryGetCellState(0, 0, out var afterResetNoTicks));
        Assert.False(afterResetNoTicks.ShouldDraw);
    }
}
