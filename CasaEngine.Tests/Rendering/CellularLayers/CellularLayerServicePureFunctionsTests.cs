using CasaEngine.Framework.Rendering.CellularLayers;
using Xunit;

namespace CasaEngine.Tests.Rendering.CellularLayers;

/// <summary>
/// Pure static functions of <see cref="CellularLayerService"/> - the period-step OR-of-signs rule
/// (docs/plan-e9d-mode-cellulaire.md §1.4), the animated source-window phase (§1.5 bis) and the
/// stateless <c>WaveX</c> formula (§1.5 ter), including both PSX signed-division rounding fixups.
/// </summary>
public class CellularLayerServicePureFunctionsTests
{
    // ---- ComputePeriodStepOr (§1.4 - OR of signs, NOT the sibling's XOR-once-at-load) --------------

    [Theory]
    [InlineData(1, 10, 1)]    // both non-negative -> +1.
    [InlineData(-1, 10, -1)]  // delta negative -> -1.
    [InlineData(1, -10, -1)]  // period negative -> -1.
    [InlineData(-1, -10, -1)] // BOTH negative -> OR still gives -1 (the sibling's XOR would give +1 -
                               // this is the one case where the two modes diverge, per §1.4).
    public void ComputePeriodStepOr_IsAnOrOfSigns(int delta, int period, int expected)
    {
        Assert.Equal(expected, CellularLayerService.ComputePeriodStepOr(delta, period));
    }

    // ---- ComputePhase / ComputeSourceV (§1.5 bis) ---------------------------------------------------

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 64)]  // 1 << 8 = 256; 256 / 4 = 64.
    [InlineData(2, 4, 128)]
    [InlineData(3, 4, 192)]
    [InlineData(0, 0, 0)]   // defensive: never measured in the shipped corpus, phase 0 instead of a divide-by-zero.
    public void ComputePhase_TruncatesIntegerDivision(int animFrameCounter, int animNum, int expected)
    {
        Assert.Equal(expected, CellularLayerService.ComputePhase(animFrameCounter, animNum));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 5, 15)]
    [InlineData(200, 100, 44)]  // 300 & 0xFF = 44: wraps past 255.
    [InlineData(255, 1, 0)]     // 256 & 0xFF = 0.
    public void ComputeSourceV_WrapsModulo256(int v0, int phase, int expected)
    {
        Assert.Equal(expected, CellularLayerService.ComputeSourceV(v0, phase));
    }

    // ---- ComputeWaveDraw (§1.5 ter - both += 0x7F fixups exercised, hand-computed) -------------------

    [Fact]
    public void ComputeWaveDraw_AWaveNegative_AppliesItsOwnFixup_TSumStaysNonNegative()
    {
        // idxA1 = (0*0)&0xFF = 0, idxA2 = (1*1)&0xFF = 1: lut[0]=-3, lut[1]=5, amp=2.
        //   aW = -3*5*2 = -30 -> negative -> += 0x7F -> 97. aW >> 7 = 0.
        // idxB = (0*0 + 1*1)&0xFF = 1: lut[1]=5, weight=-1 -> bW = -5.
        //   tSum = 0 + (-5) = -5 -> negative -> += 0x7F -> 122 (still non-negative, no further fixup).
        // x = 100 + (122 >> 7) - 8 = 100 + 0 - 8 = 92. y = y0 = 0.
        var waveLut = new int[256];
        waveLut[0] = -3;
        waveLut[1] = 5;

        CellularLayerService.ComputeWaveDraw(
            waveLut, x0: 100, y0: 0, waveTick: 1,
            aWaveY: 0, aWavePhase: 1, aWaveAmp: 2,
            bWaveY: 0, bWavePhase: 1, bWaveWeight: -1,
            out var x, out var y);

        Assert.Equal(92, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void ComputeWaveDraw_TSumNegativeAfterAWaveIsPositive_AppliesItsOwnFixup()
    {
        // idxA1 = (5*1)&0xFF = 5, idxA2 = (3*1)&0xFF = 3: lut[5]=4, lut[3]=2, amp=5.
        //   aW = 4*2*5 = 40 -> non-negative, no fixup. aW >> 7 = 0.
        // idxB = (5*2 + 3*1)&0xFF = 13: lut[13]=30, weight=-10 -> bW = -300.
        //   tSum = 0 + (-300) = -300 -> negative -> += 0x7F -> -173 (the single fixup does not fully
        //   correct a large-magnitude negative - it is a rounding correction, not a modulo).
        // tSum >> 7 (arithmetic shift of -173) = -2 (floor(-173/128)).
        // x = 50 + (-2) - 8 = 40. y = y0 = 5.
        var waveLut = new int[256];
        waveLut[5] = 4;
        waveLut[3] = 2;
        waveLut[13] = 30;

        CellularLayerService.ComputeWaveDraw(
            waveLut, x0: 50, y0: 5, waveTick: 3,
            aWaveY: 1, aWavePhase: 1, aWaveAmp: 5,
            bWaveY: 2, bWavePhase: 1, bWaveWeight: -10,
            out var x, out var y);

        Assert.Equal(40, x);
        Assert.Equal(5, y);
    }

    [Fact]
    public void ComputeWaveDraw_YIsAlwaysY0Unchanged()
    {
        var waveLut = new int[256];
        waveLut[0] = 7;

        CellularLayerService.ComputeWaveDraw(
            waveLut, x0: 0, y0: 123, waveTick: 0,
            aWaveY: 0, aWavePhase: 0, aWaveAmp: 1,
            bWaveY: 0, bWavePhase: 0, bWaveWeight: 1,
            out _, out var y);

        Assert.Equal(123, y);
    }
}
