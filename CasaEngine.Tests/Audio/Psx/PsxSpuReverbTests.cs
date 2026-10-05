using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Psx;

/// <summary>
/// Tests of the SPU reverb unit (T4.3). The expectations come from a model written here from the psx-spx "Reverb
/// Formula" (word indexed memory, independent of the production addressing), with synthetic FIR coefficients and
/// synthetic register sets: no hardware table is used or shipped.
/// </summary>
public class PsxSpuReverbTests
{
    private const int Esa = 0xFF00; // work area start in 8 byte units: 7F800h..7FFFFh, 256 units, 1024 words
    private const int ImpulseUnit = 0x100;
    private const int Frames = 3000;
    private const int Taps = 39;

    private static readonly int[] Positive = { 0, 16, 32, 48, -24 };
    private static readonly int[] Negative = { 0, -8, -16, 12, 20 };

    // Both sets share one layout (offsets in 8 byte units inside the 256 unit work area). Writers: APF1/APF2 at 08h/10h
    // (left) and 88h/90h (right), SAME/DIFF at 60h/70h (left) and E0h/F0h (right). Each comb read, and each
    // dSAME/dDIFF read, sits just below its own side's SAME/DIFF writer, with no other writer in between, so it
    // returns data written by that side; with vAPF = 0 the output reads the comb sum stored by APF1 16 units earlier.
    // Set 1: comb, same side and different side reflections with the IIR feedback; APF volumes zero.
    private static readonly int[] CombSet =
    {
        0x10, 0x18, 0x5000, 0x3000, -0x2000, 0x1800, 0x1000, 0x3800, 0, 0,
        0x60, 0xE0, 0x30, 0xB0, 0x38, 0xB8, 0x50, 0xD0, 0x70, 0xF0,
        0x40, 0xC0, 0x68, 0xE8, 0x64, 0xE4, 0x08, 0x88, 0x10, 0x90,
        0x6000, -0x7000,
    };

    // Set 2: comb taps feeding the two all-pass filters, weak reflections.
    private static readonly int[] AllPassSet =
    {
        0x10, 0x18, 0x2000, 0x5000, 0x2800, 0, 0x1000, 0x2000, 0x4000, -0x3000,
        0x60, 0xE0, 0x30, 0xB0, 0x38, 0xB8, 0x50, 0xD0, 0x70, 0xF0,
        0x40, 0xC0, 0x68, 0xE8, 0x64, 0xE4, 0x08, 0x88, 0x10, 0x90,
        0x7000, 0x5000,
    };

    private static short[] SyntheticFir()
    {
        var fir = new short[Taps];
        for (var i = 0; i < Taps; i++)
        {
            fir[i] = (short)(((i * 37 + 11) % 23 - 9) * 400 + (i == 19 ? 0x3000 : 0));
        }

        return fir;
    }

    private static byte[] ImpulseBlock()
    {
        var bytes = new byte[16];
        bytes[0] = 0x00; // filter 0, shift field 0 -> nibble << 12
        bytes[1] = 0x01; // loop end without repeat: plays once then mutes
        bytes[2] = 0x07; // first sample 7000h, then zeros
        return bytes;
    }

    private static PsxSpu NewSpu(short[] fir, int[] registers, int volumeLeft, int volumeRight, bool enabled, uint eon, int voices = 1)
    {
        var spu = new PsxSpu(new PsxSpuHardwareTables(Positive, Negative, fir == null ? default : new ReadOnlySpan<short>(fir)));
        for (var v = 0; v < voices; v++)
        {
            spu.WriteRam((ImpulseUnit + v * 2) * 8, ImpulseBlock());
            spu.SetStartAddress(v, (ushort)(ImpulseUnit + v * 2));
            spu.SetPitch(v, 0x1000);
            spu.SetAdsr(v, 0x1FC0000F);
            spu.SetVolume(v, false, 0x3FFF);
            spu.SetVolume(v, true, 0x3FFF);
        }

        if (registers != null)
        {
            for (var i = 0; i < registers.Length; i++)
            {
                spu.SetReverbRegister(i, (ushort)registers[i]);
            }
        }

        spu.SetReverbWorkAreaStart(Esa);
        spu.SetReverbOutputVolume((short)volumeLeft, (short)volumeRight);
        spu.SetReverbVoices(eon);
        spu.SetReverbEnabled(enabled);
        spu.KeyOn((1u << voices) - 1);
        return spu;
    }

    private static short[] Render(PsxSpu spu, int frames)
    {
        var buffer = new short[frames * 2];
        spu.Render(buffer, frames);
        return buffer;
    }

    private static byte[] Ram(PsxSpu spu)
    {
        var ram = new byte[PsxSpu.RamSize];
        spu.ReadRam(0, ram);
        return ram;
    }

    private static int Sat(long v) => (int)Math.Clamp(v, -0x8000, 0x7FFF);

    private static int Mul(int a, int b) => Sat(((long)a * b) >> 15);

    // Model of the psx-spx reverb: the 44100 Hz input goes through the FIR and is decimated, the formula runs at 22050 Hz
    // on 16 bit words, the result is zero stuffed and goes through the FIR again, then it is added to the dry signal.
    private static short[] Model(short[] dry, int[] reg, short[] fir, int volumeLeft, int volumeRight)
    {
        var frames = dry.Length / 2;
        var mem = new int[0x40000];
        var esaWord = Esa * 4;
        var size = 0x40000 - esaWord;
        var bufferAddress = esaWord;
        var zl = new int[frames];
        var zr = new int[frames];
        var result = new short[dry.Length];

        int Wrap(int word)
        {
            var rel = (bufferAddress + word - esaWord) % size;
            return esaWord + (rel < 0 ? rel + size : rel);
        }

        int At(int registerIndex, int extraWords = 0) => Wrap(reg[registerIndex] * 4 + extraWords);

        int Filter(int[] x, int n)
        {
            long sum = 0;
            for (var i = 0; i < Taps; i++)
            {
                if (n - i >= 0)
                {
                    sum += (long)fir[i] * x[n - i];
                }
            }

            return Sat(sum >> 15);
        }

        var inL = new int[frames];
        var inR = new int[frames];
        for (var n = 0; n < frames; n++)
        {
            inL[n] = dry[n * 2];
            inR[n] = dry[n * 2 + 1];
            if (n % 2 == 0)
            {
                var lin = Mul(Filter(inL, n), reg[30]);
                var rin = Mul(Filter(inR, n), reg[31]);

                void Reflect(int input, int m, int d)
                {
                    var old = mem[At(m, -1)];
                    var t = Sat(input + Mul(mem[At(d)], reg[7]));
                    t = Sat(t - old);
                    t = Mul(t, reg[2]);
                    mem[At(m)] = Sat(t + old);
                }

                Reflect(lin, 10, 16);
                Reflect(rin, 11, 17);
                Reflect(lin, 18, 25);
                Reflect(rin, 19, 24);

                int Out(int c1, int c2, int c3, int c4)
                {
                    var o = Mul(mem[At(c1)], reg[3]);
                    o = Sat(o + Mul(mem[At(c2)], reg[4]));
                    o = Sat(o + Mul(mem[At(c3)], reg[5]));
                    return Sat(o + Mul(mem[At(c4)], reg[6]));
                }

                var lout = Out(12, 14, 20, 22);
                var rout = Out(13, 15, 21, 23);

                int Apf(int v, int m, int d, int vol)
                {
                    var delayed = mem[Wrap(reg[m] * 4 - reg[d] * 4)];
                    v = Sat(v - Mul(delayed, vol));
                    mem[At(m)] = v;
                    return Sat(Mul(v, vol) + delayed);
                }

                lout = Apf(lout, 26, 0, reg[8]);
                rout = Apf(rout, 27, 0, reg[8]);
                lout = Apf(lout, 28, 1, reg[9]);
                rout = Apf(rout, 29, 1, reg[9]);

                zl[n] = Mul(lout, volumeLeft);
                zr[n] = Mul(rout, volumeRight);
                bufferAddress = Math.Max(esaWord, (bufferAddress + 1) & 0x3FFFF);
            }
        }

        for (var n = 0; n < frames; n++)
        {
            result[n * 2] = (short)Sat(dry[n * 2] + Filter(zl, n));
            result[n * 2 + 1] = (short)Sat(dry[n * 2 + 1] + Filter(zr, n));
        }

        return result;
    }

    private static short[] Dry(uint voices = 1, int frames = Frames) => Render(NewSpu(null, null, 0, 0, false, 0, (int)voices), frames);

    private static void AssertSameSamples(short[] expected, short[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(expected[i] == actual[i], $"sample {i}: expected {expected[i]}, got {actual[i]}");
        }
    }

    private static bool Differs(short[] a, short[] b)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return true;
            }
        }

        return false;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ImpulseResponseMatchesTheFormulaModel(int set)
    {
        var registers = set == 0 ? CombSet : AllPassSet;
        var fir = SyntheticFir();
        var dry = Dry();
        var expected = Model(dry, registers, fir, 0x5000, 0x6000);

        var actual = Render(NewSpu(fir, registers, 0x5000, 0x6000, true, 1), Frames);

        var wetLeft = 0;
        var wetRight = 0;
        for (var n = 0; n < Frames; n++)
        {
            wetLeft += actual[n * 2] != dry[n * 2] ? 1 : 0;
            wetRight += actual[n * 2 + 1] != dry[n * 2 + 1] ? 1 : 0;
        }

        Assert.True(wetLeft > 100 && wetRight > 100, $"wet samples: left {wetLeft}, right {wetRight}");
        AssertSameSamples(expected, actual);
    }

    [Fact]
    public void OutputVolumesScaleTheReverbOutputIncludingNegative()
    {
        var fir = SyntheticFir();
        var dry = Dry();

        var actual = Render(NewSpu(fir, CombSet, -0x6000, 0x4000, true, 1), Frames);
        AssertSameSamples(Model(dry, CombSet, fir, -0x6000, 0x4000), actual);

        // Each channel's wet part with a negative volume is the negative image of the same positive volume (the zero
        // stuffed samples are exactly opposite; the FIR sum may differ by a unit of rounding). Left uses -6000h/+6000h,
        // right uses +4000h/-4000h.
        var other = Render(NewSpu(fir, CombSet, 0x6000, -0x4000, true, 1), Frames);
        var wet = new int[2];
        for (var n = 0; n < Frames; n++)
        {
            for (var side = 0; side < 2; side++)
            {
                var a = actual[n * 2 + side] - dry[n * 2 + side];
                var o = other[n * 2 + side] - dry[n * 2 + side];
                Assert.True(Math.Abs(a + o) <= 2, $"frame {n} side {side}: {a} vs {o}");
                wet[side] += o != 0 ? 1 : 0;
            }
        }

        Assert.True(wet[0] > 100 && wet[1] > 100, $"wet samples: left {wet[0]}, right {wet[1]}");
    }

    [Fact]
    public void ReverbNeverWritesOutsideTheWorkAreaAndWrapsInsideIt()
    {
        var spu = NewSpu(SyntheticFir(), CombSet, 0x5000, 0x5000, true, 1);
        var sentinel = new byte[Esa * 8];
        for (var i = 0; i < sentinel.Length; i++)
        {
            sentinel[i] = (byte)(i * 31 + 7);
        }

        // The impulse block sits below the work area; keep it, the sentinel covers the rest.
        var block = ImpulseBlock();
        spu.WriteRam(0, sentinel);
        spu.WriteRam(ImpulseUnit * 8, block);
        var below = Ram(spu).AsSpan(0, Esa * 8).ToArray();

        var rendered = 0;
        while (rendered < 20000) // 10000 ticks: about 10 laps of the 1024 word work area
        {
            Render(spu, 1000);
            rendered += 1000;
        }

        var after = Ram(spu);
        Assert.True(after.AsSpan(0, Esa * 8).SequenceEqual(below), "RAM below the work area changed");
        Assert.True(after.AsSpan(Esa * 8).ToArray().Any(b => b != 0), "the work area was not written");
    }

    [Fact]
    public void WithoutFirCoefficientsTheReverbIsBypassed()
    {
        var spu = NewSpu(null, CombSet, 0x5000, 0x5000, true, 1);
        var before = Ram(spu).ToArray();
        var actual = Render(spu, Frames);

        AssertSameSamples(Dry(), actual);
        Assert.True(Ram(spu).AsSpan().SequenceEqual(before));
    }

    [Fact]
    public void MasterEnableOffWritesNothingAndKeepsTheDryOutputOnAnEmptyWorkArea()
    {
        var spu = NewSpu(SyntheticFir(), CombSet, 0x5000, 0x5000, false, 1);
        var before = Ram(spu).ToArray();
        var actual = Render(spu, Frames);

        AssertSameSamples(Dry(), actual);
        Assert.True(Ram(spu).AsSpan().SequenceEqual(before));
    }

    [Fact]
    public void MasterEnableOffStillReadsTheWorkArea()
    {
        // psx-spx "Reverb Disable": reads still occur. Pre-fill the area with a pattern, output must differ from the dry one.
        var spu = NewSpu(SyntheticFir(), CombSet, 0x5000, 0x5000, false, 1);
        var pattern = new byte[0x800];
        for (var i = 0; i < pattern.Length; i++)
        {
            pattern[i] = (byte)(i * 13 + 5);
        }

        spu.WriteRam(Esa * 8, pattern);
        var actual = Render(spu, 500);

        Assert.True(Differs(Dry(1, 500), actual));
        Assert.True(Ram(spu).AsSpan(Esa * 8).SequenceEqual(pattern));
    }

    [Fact]
    public void ClearVoiceSendOrZeroOutputVolumeGivesTheDryOutput()
    {
        var fir = SyntheticFir();

        AssertSameSamples(Dry(), Render(NewSpu(fir, CombSet, 0x5000, 0x5000, true, 0), Frames));
        AssertSameSamples(Dry(), Render(NewSpu(fir, CombSet, 0, 0, true, 1), Frames));
    }

    [Fact]
    public void RenderDoesNotAllocateWithTheReverbRunning()
    {
        var spu = NewSpu(SyntheticFir(), CombSet, 0x5000, 0x5000, true, 0xFFFFFF, 24);
        for (var v = 0; v < 24; v++)
        {
            spu.SetAdsr(v, 0x1FC0000F);
        }

        var buffer = new short[441 * 2];
        for (var i = 0; i < 50; i++)
        {
            spu.Render(buffer, 441); // warm-up: lets the JIT finish before the measured loop
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            spu.Render(buffer, 441);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
