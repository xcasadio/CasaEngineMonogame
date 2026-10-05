using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Psx;

/// <summary>
/// Synthetic tests of the PlayStation SPU core. Expected values are recomputed here from the psx-spx formulas, with
/// synthetic hardware tables defined in this file (no table of the real hardware is used or shipped).
/// </summary>
public class PsxSpuTests
{
    // Synthetic ADPCM filter coefficients (1/64 units), deliberately not the hardware ones.
    private static readonly int[] Positive = { 0, 16, 32, 48, -24 };
    private static readonly int[] Negative = { 0, -8, -16, 12, 20 };

    private const int Delay = 3; // frames before the first decoded sample reaches the cubic output at pitch 1000h
    private const int MaxVolumeRegister = 0x3FFF; // direct volume register: level +7FFEh

    private sealed record Block(int Shift, int Filter, int Flags, int[] Nibbles);

    private static PsxSpuHardwareTables Tables() => new(Positive, Negative);

    private static Block MakeBlock(int shift, int filter, int flags, int seed)
    {
        var nibbles = new int[28];
        var state = (uint)(seed * 2654435761u + 12345u);
        for (var i = 0; i < nibbles.Length; i++)
        {
            state = state * 1664525u + 1013904223u;
            nibbles[i] = (int)((state >> 24) & 0xF) - 8; // -8..7
        }

        return new Block(shift, filter, flags, nibbles);
    }

    private static byte[] ToBytes(Block block)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)((block.Filter << 4) | block.Shift);
        bytes[1] = (byte)block.Flags;
        for (var j = 0; j < 28; j++)
        {
            var nibble = block.Nibbles[j] & 0xF;
            bytes[2 + j / 2] |= (byte)((j & 1) == 0 ? nibble : nibble << 4);
        }

        return bytes;
    }

    // psx-spx formula: s = (t << (12 - shift)) + ((old*f0 + older*f1 + 32) / 64), clamped to 16 bit.
    private static short[] DecodeSequence(IEnumerable<Block> blocks)
    {
        var samples = new List<short>();
        var old = 0;
        var older = 0;
        foreach (var block in blocks)
        {
            foreach (var t in block.Nibbles)
            {
                var s = (t << (12 - block.Shift)) + ((old * Positive[block.Filter] + older * Negative[block.Filter] + 32) / 64);
                s = Math.Clamp(s, -0x8000, 0x7FFF);
                samples.Add((short)s);
                older = old;
                old = s;
            }
        }

        return samples.ToArray();
    }

    private static int Scale(int sample, int level)
    {
        var lvol = (0x7FFF * level) >> 15;
        return Math.Clamp((sample * lvol) >> 15, -0x8000, 0x7FFF);
    }

    private static ushort VolumeRegisterFor(int level) => (ushort)((level / 2) & 0x7FFF);

    private static PsxSpu NewSpu(int voice, int unitAddress, IEnumerable<Block> blocks, ushort pitch, out int blockCount)
    {
        var spu = new PsxSpu(Tables());
        var address = unitAddress * 8;
        blockCount = 0;
        foreach (var block in blocks)
        {
            spu.WriteRam(address, ToBytes(block));
            address += 16;
            blockCount++;
        }

        spu.SetStartAddress(voice, (ushort)unitAddress);
        spu.SetPitch(voice, pitch);
        spu.SetVolume(voice, false, MaxVolumeRegister);
        spu.SetVolume(voice, true, MaxVolumeRegister);
        return spu;
    }

    private static short[] RenderLeft(PsxSpu spu, int frames)
    {
        var buffer = new short[frames * 2];
        spu.Render(buffer, frames);
        var left = new short[frames];
        for (var i = 0; i < frames; i++)
        {
            left[i] = buffer[i * 2];
        }

        return left;
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 5)]
    [InlineData(2, 9)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(4, 12)]
    public void Adpcm_DecodesEachFilterAndShiftFromTheFormula_HistoryCrossesBlocks(int filter, int shift)
    {
        var blocks = new List<Block> { MakeBlock(shift, filter, 0, 1), MakeBlock(shift, filter, 0, 2), MakeBlock(shift, filter, 0, 3) };
        var spu = NewSpu(0, 0x100, blocks, 0x1000, out _);
        spu.KeyOn(1);
        var expectedSamples = DecodeSequence(blocks);

        var output = RenderLeft(spu, 80);

        for (var n = 0; n < output.Length; n++)
        {
            var expected = n < Delay ? 0 : Scale(expectedSamples[n - Delay], 0x7FFE);
            Assert.Equal(expected, output[n]);
        }
    }

    [Fact]
    public void Adpcm_ClampsTheDecodedSampleTo16Bit()
    {
        // filter 4 with a large positive previous sample and maximum nibbles must saturate rather than wrap.
        var blocks = new List<Block> { MakeBlock(0, 0, 0, 7), new Block(0, 2, 0, Enumerable.Repeat(7, 28).ToArray()) };
        var spu = NewSpu(0, 0x100, blocks, 0x1000, out _);
        spu.KeyOn(1);
        var expectedSamples = DecodeSequence(blocks);

        var output = RenderLeft(spu, 56 + Delay);

        Assert.Contains(expectedSamples, s => s == short.MaxValue);
        for (var n = Delay; n < output.Length; n++)
        {
            Assert.Equal(Scale(expectedSamples[n - Delay], 0x7FFE), output[n]);
        }
    }

    [Fact]
    public void Loop_StartAndEndFlagsRepeatTheMarkedBlocksAndSetEndx()
    {
        var a = MakeBlock(8, 0, 0, 11);
        var b = MakeBlock(9, 1, 4, 12); // loop start
        var c = MakeBlock(9, 2, 3, 13); // end + repeat
        var spu = NewSpu(2, 0x100, new[] { a, b, c }, 0x1000, out _);
        spu.KeyOn(1u << 2);

        Assert.Equal(0u, spu.ReadEndx());
        var output = RenderLeft(spu, 200);

        // The decoder history is kept across the jump from C back to B.
        var expectedSamples = DecodeSequence(new[] { a, b, c, b, c, b, c, b });
        for (var n = Delay; n < output.Length; n++)
        {
            Assert.Equal(Scale(expectedSamples[n - Delay], 0x7FFE), output[n]);
        }

        Assert.Equal(0x102, spu.GetRepeatAddress(2));
        Assert.Equal(1u << 2, spu.ReadEndx());

        spu.KeyOn(1u << 2);
        Assert.Equal(0u, spu.ReadEndx());
    }

    [Fact]
    public void Loop_EndWithoutRepeatJumpsToTheRepeatAddressAndMutes()
    {
        var one = MakeBlock(10, 0, 1, 21); // end, no repeat flag: end + mute
        var silent = new Block(0, 0, 3, new int[28]);
        var spu = NewSpu(0, 0x100, new[] { one }, 0x1000, out _);
        spu.WriteRam(0x200 * 8, ToBytes(silent));
        spu.SetRepeatAddress(0, 0x200); // no start flag: the register set by the software is used
        spu.KeyOn(1);
        var expectedSamples = DecodeSequence(new[] { one });

        var output = RenderLeft(spu, 60);

        for (var n = 0; n < output.Length; n++)
        {
            var expected = n < Delay || n >= 29 ? 0 : Scale(expectedSamples[n - Delay], 0x7FFE);
            Assert.Equal(expected, output[n]);
        }

        Assert.Equal(1u, spu.ReadEndx());
        Assert.Equal(0x200, spu.GetCurrentAddress(0));
        Assert.Equal(0, spu.GetEnvx(0));
    }

    [Fact]
    public void KeyOff_PlaceholderSilencesTheVoice()
    {
        var spu = NewSpu(0, 0x100, new[] { MakeBlock(11, 0, 0, 5), MakeBlock(11, 0, 0, 6) }, 0x1000, out _);
        spu.KeyOn(1);
        Assert.Contains(RenderLeft(spu, 20), s => s != 0);

        spu.KeyOff(1);

        Assert.All(RenderLeft(spu, 20), s => Assert.Equal(0, s));
    }

    [Theory]
    [InlineData(0x1000, 1)]
    [InlineData(0x2000, 2)]
    [InlineData(0x4000, 4)]
    [InlineData(0x4001, 4)]
    [InlineData(0x5000, 4)]
    [InlineData(0xFFFF, 4)]
    public void Pitch_AdvancesTheSamplesPerFrameAndClipsAbove4000h(int pitch, int samplesPerFrame)
    {
        var blocks = Enumerable.Range(0, 8).Select(i => MakeBlock(8, i % 5, 0, 30 + i)).ToList();
        var spu = NewSpu(0, 0x100, blocks, (ushort)pitch, out _);
        spu.KeyOn(1);
        var expectedSamples = DecodeSequence(blocks);

        var frames = 50;
        var output = RenderLeft(spu, frames);

        for (var n = 0; n < frames; n++)
        {
            var index = samplesPerFrame * n - Delay;
            var expected = index < 0 ? 0 : Scale(expectedSamples[index], 0x7FFE);
            Assert.Equal(expected, output[n]);
        }
    }

    [Fact]
    public void Pitch_ZeroDoesNotAdvance()
    {
        var blocks = new List<Block> { MakeBlock(9, 0, 0, 41), MakeBlock(9, 0, 0, 42) };
        var spu = NewSpu(0, 0x100, blocks, 0x1000, out _);
        spu.KeyOn(1);
        RenderLeft(spu, 10);
        var addressBefore = spu.GetCurrentAddress(0);
        var expectedSamples = DecodeSequence(blocks);

        spu.SetPitch(0, 0);
        var held = RenderLeft(spu, 100);

        // After frame 9 the history is s6..s9: the output holds the interpolated value of that history.
        Assert.All(held, s => Assert.Equal(Scale(expectedSamples[7], 0x7FFE), s));
        Assert.Equal(addressBefore, spu.GetCurrentAddress(0));
    }

    [Fact]
    public void Interpolation_UsesTheGaussianTableWhenSuppliedAndTheCubicOtherwise()
    {
        Assert.IsType<PsxCubicInterpolator>(new PsxSpu(Tables()).Interpolator);

        var gaussian = new short[512];
        gaussian[0x100] = 0x4000; // weight of "old" at index 0
        gaussian[0xFF] = 0x2000; // weight of "oldest" at index 0
        var tables = new PsxSpuHardwareTables(Positive, Negative, gaussian: gaussian);
        var spu = new PsxSpu(tables);
        Assert.IsType<PsxGaussianInterpolator>(spu.Interpolator);

        var blocks = new List<Block> { MakeBlock(9, 0, 0, 51), MakeBlock(9, 0, 0, 52) };
        spu.WriteRam(0x100 * 8, ToBytes(blocks[0]));
        spu.WriteRam(0x102 * 8, ToBytes(blocks[1]));
        spu.SetStartAddress(0, 0x100);
        spu.SetPitch(0, 0x1000);
        spu.SetVolume(0, false, MaxVolumeRegister);
        spu.KeyOn(1);
        var s = DecodeSequence(blocks);

        var output = RenderLeft(spu, 40);

        for (var n = 4; n < output.Length; n++)
        {
            // history at frame n: oldest s[n-4], older s[n-3], old s[n-2], new s[n-1]
            var expected = ((0x4000 * s[n - 2]) >> 15) + ((0x2000 * s[n - 4]) >> 15);
            Assert.Equal(Scale(expected, 0x7FFE), output[n]);
        }
    }

    [Fact]
    public void Volume_NegativeInvertsThePhaseAndFixedValuesAreScaled()
    {
        var blocks = new List<Block> { MakeBlock(9, 0, 0, 61), MakeBlock(9, 0, 0, 62) };
        var spu = NewSpu(0, 0x100, blocks, 0x1000, out _);
        spu.SetVolume(0, false, VolumeRegisterFor(-0x2000)); // left negative
        spu.SetVolume(0, true, VolumeRegisterFor(0x2000)); // right positive, same magnitude
        spu.KeyOn(1);
        var s = DecodeSequence(blocks);

        var buffer = new short[50 * 2];
        spu.Render(buffer, 50);

        Assert.Equal(-0x2000, spu.GetCurrentVolume(0, false));
        Assert.Equal(0x2000, spu.GetCurrentVolume(0, true));
        var sawNonZero = false;
        for (var n = Delay; n < 50; n++)
        {
            Assert.Equal(Scale(s[n - Delay], -0x2000), buffer[n * 2]);
            Assert.Equal(Scale(s[n - Delay], 0x2000), buffer[n * 2 + 1]);
            sawNonZero |= buffer[n * 2 + 1] != 0;
            Assert.True(buffer[n * 2] == -buffer[n * 2 + 1] || Math.Abs(buffer[n * 2] + buffer[n * 2 + 1]) <= 1);
        }

        Assert.True(sawNonZero);
    }

    [Fact]
    public void Volume_DirectRegisterRangeCoversNegativeAndPositiveExtremes()
    {
        var spu = new PsxSpu(Tables());
        spu.SetVolume(0, false, 0x4000); // -4000h * 2
        spu.SetVolume(0, true, 0x3FFF); // +3FFFh * 2
        Assert.Equal(-0x8000, spu.GetCurrentVolume(0, false));
        Assert.Equal(0x7FFE, spu.GetCurrentVolume(0, true));
    }

    // Independent plain-language version of the psx-spx envelope step, used as the expectation.
    private static int[] ExpectedSweep(int startLevel, int register, int frames)
    {
        var exponential = (register & 0x4000) != 0;
        var decrease = (register & 0x2000) != 0;
        var negative = (register & 0x1000) != 0;
        var shift = (register >> 2) & 0x1F;
        var step = register & 3;
        var level = startLevel;
        var counter = 0;
        var result = new int[frames];
        for (var f = 0; f < frames; f++)
        {
            var delta = 7 - step;
            if (decrease != negative)
            {
                delta = -delta - 1;
            }

            delta *= 1 << Math.Max(0, 11 - shift);
            var increment = 0x8000 >> Math.Max(0, shift - 11);
            if (exponential && !decrease && level > 0x6000)
            {
                if (shift < 10)
                {
                    delta /= 4;
                }
                else if (shift >= 11)
                {
                    increment /= 4;
                }
                else
                {
                    delta /= 2;
                    increment /= 2;
                }
            }
            else if (exponential && decrease)
            {
                delta = delta * level / 0x8000;
            }

            if ((step | (shift << 2)) != 0x7F)
            {
                increment = Math.Max(increment, 1);
            }

            counter += increment;
            if ((counter & 0x8000) != 0)
            {
                counter = 0;
                level += delta;
                level = !decrease ? Math.Clamp(level, -0x8000, 0x7FFF) : negative ? Math.Clamp(level, -0x8000, 0) : Math.Max(level, 0);
            }

            result[f] = level;
        }

        return result;
    }

    private static int[] RunSweep(int startLevel, int register, int frames)
    {
        var spu = new PsxSpu(Tables());
        spu.SetVolume(0, false, VolumeRegisterFor(startLevel));
        spu.SetVolume(0, false, (ushort)register);
        var levels = new int[frames];
        var buffer = new short[2];
        for (var f = 0; f < frames; f++)
        {
            spu.Render(buffer, 1);
            levels[f] = spu.GetCurrentVolume(0, false);
        }

        return levels;
    }

    private static ushort Sweep(bool exponential, bool decrease, bool negative, int shift, int step) =>
        (ushort)(0x8000 | (exponential ? 0x4000 : 0) | (decrease ? 0x2000 : 0) | (negative ? 0x1000 : 0) | (shift << 2) | step);

    [Fact]
    public void Sweep_LinearIncreaseAddsTheStepEveryFrameAtShift11()
    {
        // step 0 => +7, shift 11 => one step per frame: the level is 7 * frames.
        var levels = RunSweep(0, Sweep(false, false, false, 11, 0), 20);
        for (var f = 0; f < levels.Length; f++)
        {
            Assert.Equal(7 * (f + 1), levels[f]);
        }
    }

    [Fact]
    public void Sweep_ShiftAbove11DelaysTheSteps()
    {
        // shift 12: the counter gains 4000h per frame, a step every 2 frames; step 3 => +4.
        var levels = RunSweep(0, Sweep(false, false, false, 12, 3), 8);
        Assert.Equal(new[] { 0, 4, 4, 8, 8, 12, 12, 16 }, levels);
    }

    [Fact]
    public void Sweep_ExponentialDecreaseScalesTheStepByTheLevel()
    {
        // step 0 decreasing => -8; exponential: -8 * level / 8000h. From 4000h: -4, then -4 * ... ; hand-checked first value.
        var levels = RunSweep(0x4000, Sweep(true, true, false, 11, 0), 3);
        Assert.Equal(0x3FFC, levels[0]);
        Assert.Equal(0x3FFC + (-8 * 0x3FFC / 0x8000), levels[1]);
    }

    [Theory]
    [InlineData(false, false, false, 11, 0, 0)]
    [InlineData(false, false, false, 9, 2, 0x1000)]
    [InlineData(false, true, false, 10, 1, 0x7000)]
    [InlineData(true, false, false, 9, 0, 0x5000)] // exponential increase crossing 6000h (slower above it)
    [InlineData(true, false, false, 10, 1, 0x5000)]
    [InlineData(true, false, false, 13, 3, 0x6000)]
    [InlineData(true, true, false, 8, 2, 0x7000)]
    [InlineData(false, true, true, 11, 0, 0x1000)] // negative phase decrease: snaps up toward zero
    [InlineData(false, false, true, 11, 1, 0x2000)] // negative phase increase: goes downward
    [InlineData(false, false, false, 31, 3, 0x1000)] // all ones: never steps
    public void Sweep_FollowsTheFormulaForEveryMode(bool exponential, bool decrease, bool negative, int shift, int step, int start)
    {
        var register = Sweep(exponential, decrease, negative, shift, step);

        var levels = RunSweep(start, register, 400);

        Assert.Equal(ExpectedSweep(start, register, 400), levels);
        if (shift == 31 && step == 3)
        {
            Assert.All(levels, l => Assert.Equal(start, l));
        }
    }

    [Fact]
    public void Output_SaturatesAtTheSignedSixteenBitLimits()
    {
        var loud = new Block(0, 0, 3, Enumerable.Repeat(7, 28).ToArray()); // 7000h forever
        var loudNegative = new Block(0, 0, 3, Enumerable.Repeat(-8, 28).ToArray()); // -8000h forever
        var spu = new PsxSpu(Tables());
        spu.WriteRam(0x100 * 8, ToBytes(new Block(0, 0, 7, loud.Nibbles)));
        spu.WriteRam(0x200 * 8, ToBytes(new Block(0, 0, 7, loudNegative.Nibbles)));
        for (var v = 0; v < 12; v++)
        {
            spu.SetStartAddress(v, 0x100);
            spu.SetPitch(v, 0x1000);
            spu.SetVolume(v, false, MaxVolumeRegister);
            spu.SetVolume(v, true, MaxVolumeRegister);
        }

        for (var v = 12; v < 24; v++)
        {
            spu.SetStartAddress(v, 0x200);
            spu.SetPitch(v, 0x1000);
            spu.SetVolume(v, false, MaxVolumeRegister);
            spu.SetVolume(v, true, MaxVolumeRegister);
        }

        // 12 positive voices alone saturate at +32767.
        spu.KeyOn(0x000FFF);
        var buffer = new short[2 * 60];
        spu.Render(buffer, 60);
        Assert.Equal(short.MaxValue, buffer[2 * 59]);
        Assert.Equal(short.MaxValue, buffer[2 * 59 + 1]);

        // Add 12 negative voices on a fresh SPU state: negative alone saturates at -32768.
        var negativeOnly = new PsxSpu(Tables());
        negativeOnly.WriteRam(0x200 * 8, ToBytes(new Block(0, 0, 7, loudNegative.Nibbles)));
        for (var v = 0; v < 12; v++)
        {
            negativeOnly.SetStartAddress(v, 0x200);
            negativeOnly.SetPitch(v, 0x1000);
            negativeOnly.SetVolume(v, false, MaxVolumeRegister);
            negativeOnly.SetVolume(v, true, MaxVolumeRegister);
        }

        negativeOnly.KeyOn(0x000FFF);
        negativeOnly.Render(buffer, 60);
        Assert.Equal(short.MinValue, buffer[2 * 59]);
        Assert.Equal(short.MinValue, buffer[2 * 59 + 1]);
    }

    [Fact]
    public void Ram_WrapsAroundThe512KbAndBlocksCanStraddleTheEnd()
    {
        var block = MakeBlock(9, 0, 0, 71);
        var spu = new PsxSpu(Tables());
        var bytes = ToBytes(block);
        // The block starts 8 bytes before the end: its second half wraps to address 0 (addresses masked).
        spu.WriteRam(PsxSpu.RamSize - 8, bytes);
        spu.SetStartAddress(0, 0xFFFF);
        spu.SetPitch(0, 0x1000);
        spu.SetVolume(0, false, MaxVolumeRegister);
        spu.KeyOn(1);
        var s = DecodeSequence(new[] { block });

        var output = RenderLeft(spu, 31);

        for (var n = Delay; n < 28 + Delay; n++)
        {
            Assert.Equal(Scale(s[n - Delay], 0x7FFE), output[n]);
        }
    }

    [Fact]
    public void Tables_InvalidSizesOrValuesThrow()
    {
        Assert.Throws<ArgumentException>(() => new PsxSpuHardwareTables(new int[4], Negative));
        Assert.Throws<ArgumentException>(() => new PsxSpuHardwareTables(Positive, new int[6]));
        Assert.Throws<ArgumentException>(() => new PsxSpuHardwareTables(new[] { 0, 0, 0, 0, 128 }, Negative));
        Assert.Throws<ArgumentException>(() => new PsxSpuHardwareTables(Positive, new[] { -129, 0, 0, 0, 0 }));
        Assert.Throws<ArgumentException>(() => new PsxSpuHardwareTables(Positive, Negative, reverbFir: new short[38]));
        Assert.Throws<ArgumentException>(() => new PsxSpuHardwareTables(Positive, Negative, gaussian: new short[511]));
        Assert.Throws<ArgumentException>(() => new PsxGaussianInterpolator(Tables()));

        var valid = new PsxSpuHardwareTables(Positive, Negative, new short[39], new short[512]);
        Assert.True(valid.HasReverbFir);
        Assert.True(valid.HasGaussianTable);
    }

    [Fact]
    public void Render_DoesNotAllocateWith24VoicesOn()
    {
        var spu = new PsxSpu(Tables());
        var blocks = new[] { MakeBlock(9, 3, 4, 81), MakeBlock(9, 1, 0, 82), MakeBlock(8, 4, 3, 83) };
        var address = 0x100 * 8;
        foreach (var block in blocks)
        {
            spu.WriteRam(address, ToBytes(block));
            address += 16;
        }

        for (var v = 0; v < PsxSpu.VoiceCount; v++)
        {
            spu.SetStartAddress(v, 0x100);
            spu.SetPitch(v, (ushort)(0x0800 + v * 0x300));
            spu.SetVolume(v, false, 0x2000);
            spu.SetVolume(v, true, v == 5 ? Sweep(true, true, false, 14, 1) : (ushort)0x2000);
        }

        spu.KeyOn(0xFFFFFF);
        var buffer = new short[441 * 2];
        spu.Render(buffer, 441); // warm-up

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            spu.Render(buffer, 441);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
