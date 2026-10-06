using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Psx;

/// <summary>
/// Tests of the default SPU tables (T7.1, D25). ADPCM expectations: psx-spx, "Pos/neg Tables" of the CD-XA ADPCM
/// section (https://psx-spx.consoledev.net/ps1/cdr/cdromformat/). The FIR is checked on its frequency response.
/// </summary>
public class PsxSpuDefaultTablesTests
{
    private static readonly int[] SourcePositive = { 0, 60, 115, 98, 122 };
    private static readonly int[] SourceNegative = { 0, 0, -52, -55, -60 };

    private const double SampleRate = 44100.0;

    private static short[] Fir() => PsxSpuDefaultReverbFir.Coefficients;

    private static double GainDb(double frequency)
    {
        var fir = Fir();
        double re = 0, im = 0;
        for (var i = 0; i < fir.Length; i++)
        {
            var w = 2.0 * Math.PI * frequency / SampleRate * i;
            re += fir[i] / 32768.0 * Math.Cos(w);
            im -= fir[i] / 32768.0 * Math.Sin(w);
        }

        return 20.0 * Math.Log10(Math.Sqrt(re * re + im * im));
    }

    [Fact]
    public void CreateDefault_HasTheFirAndNoGaussianTable()
    {
        var tables = PsxSpuHardwareTables.CreateDefault();

        Assert.True(tables.HasReverbFir);
        Assert.False(tables.HasGaussianTable);
        Assert.Equal(Fir(), tables.ReverbFir);
    }

    [Fact]
    public void CreateDefault_AdpcmCoefficientsEqualTheSourceTable()
    {
        var tables = PsxSpuHardwareTables.CreateDefault();

        for (var f = 0; f < PsxSpuHardwareTables.AdpcmFilterCount; f++)
        {
            Assert.Equal(SourcePositive[f], tables.AdpcmPositive(f));
            Assert.Equal(SourceNegative[f], tables.AdpcmNegative(f));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DefaultTables_DecodeABlockOfEachFilterFromTheFormula(int filter)
    {
        var spu = new PsxSpu(PsxSpuHardwareTables.CreateDefault());
        var bytes = new byte[16];
        bytes[0] = (byte)((filter << 4) | 8);
        bytes[1] = 1; // loop end, no repeat
        var nibbles = new int[28];
        var state = 77u + (uint)filter;
        for (var i = 0; i < 28; i++)
        {
            state = state * 1664525u + 1013904223u;
            nibbles[i] = (int)((state >> 24) & 0xF) - 8;
            bytes[2 + i / 2] |= (byte)((i & 1) == 0 ? nibbles[i] & 0xF : (nibbles[i] & 0xF) << 4);
        }

        spu.WriteRam(0x100 * 8, bytes);
        spu.SetStartAddress(0, 0x100);
        spu.SetPitch(0, 0x1000);
        spu.SetAdsr(0, 0x1FC0000F);
        spu.SetVolume(0, false, 0x3FFF);
        spu.SetVolume(0, true, 0x3FFF);
        spu.KeyOn(1);
        var buffer = new short[40 * 2];
        spu.Render(buffer, 40);

        var expected = new int[28];
        int old = 0, older = 0;
        for (var i = 0; i < 28; i++)
        {
            var s = (nibbles[i] << (12 - 8)) + ((old * SourcePositive[filter] + older * SourceNegative[filter] + 32) / 64);
            s = Math.Clamp(s, -0x8000, 0x7FFF);
            expected[i] = s;
            older = old;
            old = s;
        }

        for (var n = 3; n < 3 + 25; n++)
        {
            var lvol = (Math.Min(0x7FFF, 0x3800 * n) * 0x7FFE) >> 15;
            var want = Math.Clamp((expected[n - 3] * lvol) >> 15, -0x8000, 0x7FFF);
            Assert.Equal(want, buffer[n * 2]);
        }
    }

    [Fact]
    public void Fir_Has39SymmetricCoefficientsSummingTo32768()
    {
        var fir = Fir();

        Assert.Equal(39, fir.Length);
        for (var i = 0; i < fir.Length; i++)
        {
            Assert.Equal(fir[i], fir[fir.Length - 1 - i]);
        }

        Assert.Equal(32768, fir.Sum(c => (int)c));
    }

    [Fact]
    public void Fir_FrequencyResponseIsALowPassAtHalfNyquist()
    {
        Assert.InRange(GainDb(0), -0.001, 0.001);
        Assert.InRange(GainDb(5512.5), -0.1, 0.1);
        Assert.InRange(GainDb(11025), -6.5, -5.5);
        for (var f = 14000.0; f <= 22050.0; f += 50.0)
        {
            Assert.True(GainDb(f) <= -60.0, $"gain at {f} Hz is {GainDb(f):F2} dB");
        }
    }

    [Fact]
    public void Reverb_ImpulseResponseWithTheDefaultTablesIsNonZeroAndDecreasing()
    {
        var spu = new PsxSpu(PsxSpuHardwareTables.CreateDefault());
        var block = new byte[16];
        block[1] = 0x01;
        block[2] = 0x07; // one sample of 7000h
        spu.WriteRam(0x100 * 8, block);
        spu.SetStartAddress(0, 0x100);
        spu.SetPitch(0, 0x1000);
        spu.SetAdsr(0, 0x1FC0000F);
        spu.SetVolume(0, false, 0x3FFF);
        spu.SetVolume(0, true, 0x3FFF);

        // Single comb loop on each side: the reflection reads its own previous write, scaled by vIIR (about 1/2).
        int[] registers =
        {
            0x10, 0x18, 0x5000, 0x3000, 0, 0, 0, 0x3800, 0, 0,
            0x60, 0xE0, 0x30, 0xB0, 0x38, 0xB8, 0x50, 0xD0, 0x70, 0xF0,
            0x40, 0xC0, 0x68, 0xE8, 0x64, 0xE4, 0x08, 0x88, 0x10, 0x90,
            0x6000, 0x6000,
        };
        for (var i = 0; i < registers.Length; i++)
        {
            spu.SetReverbRegister(i, (ushort)registers[i]);
        }

        spu.SetReverbWorkAreaStart(0xFF00);
        spu.SetReverbOutputVolume(0x5000, 0x5000);
        spu.SetReverbVoices(1);
        spu.SetReverbEnabled(true);
        spu.KeyOn(1);

        const int frames = 4000;
        var buffer = new short[frames * 2];
        spu.Render(buffer, frames);

        // Peak per window of 500 frames: reverb tail energy must exist and fall.
        var peaks = new int[frames / 500];
        for (var n = 0; n < frames; n++)
        {
            peaks[n / 500] = Math.Max(peaks[n / 500], Math.Abs((int)buffer[n * 2]));
        }

        Assert.True(peaks.Max() > 0);
        var top = Array.IndexOf(peaks, peaks.Max());
        for (var w = top + 1; w < peaks.Length; w++)
        {
            Assert.True(peaks[w] <= peaks[w - 1], $"window {w}: {peaks[w]} > {peaks[w - 1]}");
        }

        Assert.True(peaks[^1] < peaks[top]);
    }

    [Fact]
    public void RenderDoesNotAllocateWithTheDefaultTables()
    {
        var spu = new PsxSpu(PsxSpuHardwareTables.CreateDefault());
        var block = new byte[16];
        block[1] = 7;
        Array.Fill(block, (byte)0x77, 2, 14);
        spu.WriteRam(0x100 * 8, block);
        spu.SetStartAddress(0, 0x100);
        spu.SetPitch(0, 0x1000);
        spu.SetAdsr(0, 0x1FC0000F);
        spu.SetVolume(0, false, 0x3FFF);
        spu.SetVolume(0, true, 0x3FFF);
        spu.SetReverbWorkAreaStart(0xFF00);
        spu.SetReverbOutputVolume(0x5000, 0x5000);
        spu.SetReverbVoices(1);
        spu.SetReverbEnabled(true);
        spu.KeyOn(1);

        var buffer = new short[441 * 2];
        for (var i = 0; i < 50; i++)
        {
            spu.Render(buffer, 441);
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            spu.Render(buffer, 441);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
