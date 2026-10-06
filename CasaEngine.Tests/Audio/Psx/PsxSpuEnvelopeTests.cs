using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Psx;

// Tests of task T4.2: ADSR, ENVX, noise generator and pitch modulation. The expectations are recomputed here from the
// psx-spx formulas (https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/), independently of PsxSpu.
public partial class PsxSpuTests
{
    // Test interpolator: the newest decoded sample, so the expected output follows the pushed samples directly.
    private sealed class NewestSampleInterpolator : IPsxSpuInterpolator
    {
        public int Interpolate(int oldest, int older, int old, int newest, int index) => newest;
    }

    private static uint Adsr(bool attackExp, int attackShift, int attackStep, int decayShift, int sustainLevel,
        bool sustainExp, bool sustainDecrease, int sustainShift, int sustainStep, bool releaseExp, int releaseShift)
    {
        uint low = (uint)((attackExp ? 0x8000 : 0) | (attackShift << 10) | (attackStep << 8) | (decayShift << 4) | sustainLevel);
        uint high = (uint)((sustainExp ? 0x8000 : 0) | (sustainDecrease ? 0x4000 : 0) | (sustainShift << 8) | (sustainStep << 6)
                           | (releaseExp ? 0x20 : 0) | releaseShift);
        return (high << 16) | low;
    }

    // Plain psx-spx envelope model: "Envelope Operation" evaluated from scratch on every tick.
    private sealed class EnvelopeModel
    {
        private readonly uint _word;
        private int _counter;
        private int _phase; // 0 attack, 1 decay, 2 sustain, 3 release

        public int Level;

        public EnvelopeModel(uint word) => _word = word;

        public void KeyOff() => _phase = 3;

        public void Tick()
        {
            var sustainLevel = ((int)(_word & 0xF) + 1) * 0x800;
            if (_phase == 0 && Level >= 0x7FFF)
            {
                _phase = 1;
            }

            if (_phase == 1 && Level <= sustainLevel)
            {
                _phase = 2;
            }

            bool exponential, decrease;
            int shift, step;
            switch (_phase)
            {
                case 0:
                    exponential = (_word & 0x8000) != 0;
                    decrease = false;
                    shift = (int)(_word >> 10) & 0x1F;
                    step = (int)(_word >> 8) & 3;
                    break;
                case 1:
                    exponential = true;
                    decrease = true;
                    shift = (int)(_word >> 4) & 0xF;
                    step = 0;
                    break;
                case 2:
                    exponential = (_word & 0x80000000) != 0;
                    decrease = (_word & 0x40000000) != 0;
                    shift = (int)(_word >> 24) & 0x1F;
                    step = (int)(_word >> 22) & 3;
                    break;
                default:
                    exponential = (_word & 0x200000) != 0;
                    decrease = true;
                    shift = (int)(_word >> 16) & 0x1F;
                    step = 0;
                    break;
            }

            var adsrStep = 7 - step;
            if (decrease)
            {
                adsrStep = -adsrStep - 1; // NOT
            }

            adsrStep *= 1 << Math.Max(0, 11 - shift);
            var increment = 0x8000 >> Math.Max(0, shift - 11);
            if (exponential && !decrease && Level > 0x6000)
            {
                if (shift < 10)
                {
                    adsrStep /= 4;
                }
                else if (shift >= 11)
                {
                    increment /= 4;
                }
                else
                {
                    adsrStep /= 2;
                    increment /= 2;
                }
            }
            else if (exponential && decrease)
            {
                adsrStep = adsrStep * Level / 0x8000;
            }

            if ((step | (shift << 2)) != 0x7F)
            {
                increment = Math.Max(increment, 1);
            }

            _counter += increment;
            if ((_counter & 0x8000) == 0)
            {
                return;
            }

            _counter = 0;
            Level += adsrStep;
            Level = decrease ? Math.Max(Level, 0) : Math.Min(Level, 0x7FFF);
        }
    }

    // A looping block (start + end + repeat) so that the voice keeps producing samples.
    private static PsxSpu LoopingVoiceSpu(uint adsr, out short[] samples, int frames)
    {
        var block = MakeBlock(10, 0, 7, 99);
        var spu = new PsxSpu(Tables());
        spu.WriteRam(0x100 * 8, ToBytes(block));
        spu.SetStartAddress(0, 0x100);
        spu.SetPitch(0, 0x1000);
        spu.SetAdsr(0, adsr);
        spu.SetVolume(0, false, MaxVolumeRegister);
        samples = DecodeSequence(Enumerable.Repeat(block, frames / 28 + 2));
        return spu;
    }

    public static IEnumerable<object[]> EnvelopeWords()
    {
        yield return new object[] { Adsr(false, 0, 0, 0, 0xF, false, false, 31, 3, false, 0), -1 }; // fast linear attack, never steps
        yield return new object[] { Adsr(false, 12, 2, 0, 0xF, false, false, 31, 3, false, 12), 900 }; // slow linear attack
        yield return new object[] { Adsr(true, 9, 1, 3, 0x4, false, false, 31, 3, true, 9), 1300 }; // exponential attack, decay to 5*800h
        yield return new object[] { Adsr(true, 13, 3, 5, 0x0, false, true, 8, 1, false, 10), 700 }; // decay to the lowest level, linear sustain decrease
        yield return new object[] { Adsr(false, 5, 0, 4, 0x8, true, true, 9, 2, true, 8), 900 }; // exponential sustain decrease
        yield return new object[] { Adsr(false, 2, 1, 6, 0x3, false, false, 10, 0, true, 11), 1000 }; // linear sustain increase to 7FFFh
        yield return new object[] { Adsr(false, 1, 2, 7, 0x2, true, false, 11, 1, false, 14), 1200 }; // exponential sustain increase
        yield return new object[] { Adsr(false, 3, 0, 15, 0x1, true, false, 13, 3, true, 12), 1400 }; // slowest decay, slow exponential sustain
        yield return new object[] { Adsr(false, 0, 3, 2, 0xF, false, false, 0, 0, false, 31), 600 }; // slowest release (shift 1Fh, step 0)
        yield return new object[] { Adsr(false, 31, 3, 0, 0xF, false, false, 0, 0, false, 5), 300 }; // attack 7Fh: never steps, stays at 0
    }

    [Theory]
    [MemberData(nameof(EnvelopeWords))]
    public void Adsr_EnvxAndOutputFollowTheFormulaFrameByFrame(uint word, int keyOffFrame)
    {
        const int frames = 1500;
        var spu = LoopingVoiceSpu(word, out var samples, frames);
        spu.KeyOn(1);
        var model = new EnvelopeModel(word);
        var buffer = new short[2];

        for (var n = 0; n < frames; n++)
        {
            if (n == keyOffFrame)
            {
                spu.KeyOff(1);
                model.KeyOff();
            }

            var envBefore = model.Level;
            Assert.Equal(envBefore, spu.GetEnvx(0));
            spu.Render(buffer, 1);
            var expected = n < Delay ? 0 : Scale(samples[n - Delay], 0x7FFE, envBefore);
            Assert.Equal(expected, buffer[0]);
            model.Tick();
            Assert.Equal(model.Level, spu.GetEnvx(0));
            Assert.InRange(spu.GetEnvx(0), 0, 0x7FFF);
        }
    }

    [Fact]
    public void Adsr_KeyOnRestartsTheAttackFromZero()
    {
        var word = Adsr(false, 4, 0, 0, 0xF, false, false, 31, 3, false, 0);
        var spu = LoopingVoiceSpu(word, out _, 100);
        spu.KeyOn(1);
        Assert.Equal(0, spu.GetEnvx(0));
        RenderLeft(spu, 200);
        Assert.Equal(0x7FFF, spu.GetEnvx(0));

        spu.KeyOn(1);

        Assert.Equal(0, spu.GetEnvx(0));
    }

    [Fact]
    public void Adsr_KeyOffDuringAttackReleasesFromTheCurrentLevel()
    {
        // Attack shift 12 step 2: +5 << 0 every 2 frames... the level after 40 frames is computed by the model.
        var word = Adsr(false, 12, 2, 0, 0xF, false, false, 31, 3, false, 9);
        var spu = LoopingVoiceSpu(word, out _, 100);
        spu.KeyOn(1);
        RenderLeft(spu, 40);
        var peak = spu.GetEnvx(0);
        Assert.InRange(peak, 1, 0x7FFE);

        spu.KeyOff(1);
        var model = new EnvelopeModel(word) { Level = peak };
        model.KeyOff();
        for (var n = 0; n < 700; n++)
        {
            RenderLeft(spu, 1);
            model.Tick();
            Assert.Equal(model.Level, spu.GetEnvx(0));
        }

        Assert.Equal(0, spu.GetEnvx(0));
        Assert.True(peak < 0x7FFF);
    }

    [Fact]
    public void Adsr_EndMuteSetsTheEnvelopeToZeroAndItStaysThere()
    {
        var one = MakeBlock(10, 0, 1, 21); // end, no repeat flag: end + mute
        var silent = new Block(0, 0, 3, new int[28]);
        var spu = NewSpu(0, 0x100, new[] { one }, 0x1000, out _);
        spu.WriteRam(0x200 * 8, ToBytes(silent));
        spu.SetRepeatAddress(0, 0x200);
        spu.KeyOn(1);

        RenderLeft(spu, 20);
        Assert.Equal(0x7FFF, spu.GetEnvx(0));
        RenderLeft(spu, 20);

        Assert.Equal(0, spu.GetEnvx(0));
        RenderLeft(spu, 50);
        Assert.Equal(0, spu.GetEnvx(0));
    }

    // psx-spx noise generator, written out in the test.
    private sealed class NoiseModel
    {
        private readonly int _step;
        private readonly int _shift;
        private int _timer;
        public int Level;

        public NoiseModel(int shift, int step)
        {
            _shift = shift;
            _step = 4 + step;
        }

        public void Tick()
        {
            _timer -= _step;
            var parity = ((Level >> 15) ^ (Level >> 12) ^ (Level >> 11) ^ (Level >> 10) ^ 1) & 1;
            if (_timer < 0)
            {
                Level = ((Level << 1) + parity) & 0xFFFF;
            }

            if (_timer < 0)
            {
                _timer += 0x20000 >> _shift;
            }

            if (_timer < 0)
            {
                _timer += 0x20000 >> _shift;
            }
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 3)]
    [InlineData(15, 1)]
    [InlineData(11, 2)]
    public void Noise_LevelSequenceAndVoiceOutputFollowThePseudoCode(int shift, int step)
    {
        var blocks = new List<Block> { MakeBlock(9, 0, 0, 1), MakeBlock(9, 1, 0, 2), MakeBlock(9, 2, 3, 3) };
        var noisy = NewSpu(0, 0x100, blocks, 0x1000, out _);
        var plain = NewSpu(0, 0x100, blocks, 0x1000, out _);
        noisy.SetNoiseClock(shift, step);
        noisy.SetNoiseMode(1);
        noisy.KeyOn(1);
        plain.KeyOn(1);
        var model = new NoiseModel(shift, step);
        var buffer = new short[2];
        var sawChange = false;

        for (var n = 0; n < 400; n++)
        {
            model.Tick();
            noisy.Render(buffer, 1);
            plain.Render(new short[2], 1);

            var level = (short)model.Level;
            Assert.Equal(level, noisy.GetNoiseLevel());
            Assert.Equal(Scale(level, 0x7FFE, FullEnvelopeAt(n)), buffer[0]);
            // The ADPCM side keeps running as if the voice was not in noise mode.
            Assert.Equal(plain.GetCurrentAddress(0), noisy.GetCurrentAddress(0));
            Assert.Equal(plain.ReadEndx(), noisy.ReadEndx());
            sawChange |= level != 0;
        }

        Assert.True(sawChange);
        Assert.Equal(1u, noisy.ReadEndx());
    }

    [Fact]
    public void Noise_DifferentClocksGiveDifferentSequences()
    {
        var fast = new PsxSpu(Tables());
        var slow = new PsxSpu(Tables());
        fast.SetNoiseClock(0, 0);
        slow.SetNoiseClock(15, 3);
        var buffer = new short[2];
        var differs = false;
        for (var n = 0; n < 300; n++)
        {
            fast.Render(buffer, 1);
            slow.Render(buffer, 1);
            differs |= fast.GetNoiseLevel() != slow.GetNoiseLevel();
        }

        Assert.True(differs);
        Assert.Throws<ArgumentOutOfRangeException>(() => fast.SetNoiseClock(16, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fast.SetNoiseClock(0, 4));
    }

    [Fact]
    public void Pmon_ModulatesTheStepWithThePreviousVoiceOutputAndIgnoresVoiceZero()
    {
        var b0 = MakeBlock(10, 0, 7, 301);
        var b1 = MakeBlock(10, 0, 7, 302);
        const int pitch1 = 0x2000;
        const int frames = 300;

        PsxSpu Build(uint pmon)
        {
            var spu = new PsxSpu(Tables(), new NewestSampleInterpolator());
            spu.WriteRam(0x100 * 8, ToBytes(b0));
            spu.WriteRam(0x200 * 8, ToBytes(b1));
            spu.SetStartAddress(0, 0x100);
            spu.SetStartAddress(1, 0x200);
            spu.SetPitch(0, 0x1000);
            spu.SetPitch(1, pitch1);
            spu.SetAdsr(0, FullEnvelope);
            spu.SetAdsr(1, FullEnvelope);
            spu.SetVolume(0, false, 0); // voice 0 is heard on the right only: left is voice 1
            spu.SetVolume(0, true, MaxVolumeRegister);
            spu.SetVolume(1, false, MaxVolumeRegister);
            spu.SetVolume(1, true, 0);
            spu.SetPitchModulation(pmon);
            spu.KeyOn(3);
            return spu;
        }

        var s0 = DecodeSequence(Enumerable.Repeat(b0, frames / 28 + 2));
        var s1 = DecodeSequence(Enumerable.Repeat(b1, frames * 4 / 28 + 4));
        var modulated = Build(3); // bit 0 must be ignored
        var reference = Build(2);
        var bufferA = new short[2];
        var bufferB = new short[2];

        var counter1 = 0;
        var pushed1 = 0;
        var sawNegativeFactor = false;
        var sawModulatedStep = false;
        for (var n = 0; n < frames; n++)
        {
            modulated.Render(bufferA, 1);
            reference.Render(bufferB, 1);

            // voice 0: pitch 1000h, never modulated, one new sample per frame.
            var env = FullEnvelopeAt(n);
            var sample0 = n == 0 ? 0 : s0[n - 1];
            Assert.Equal(Scale(sample0, 0x7FFE, env), bufferA[1]);
            Assert.Equal(bufferB[1], bufferA[1]);

            // voice 1: the output uses the samples pushed so far.
            var sample1 = pushed1 == 0 ? 0 : s1[pushed1 - 1];
            Assert.Equal(Scale(sample1, 0x7FFE, env), bufferA[0]);
            Assert.Equal(bufferB[0], bufferA[0]); // same samples at frame n: the sequences diverge only through the step

            var outx0 = (sample0 * env) >> 15;
            var factor = outx0 + 0x8000;
            sawNegativeFactor |= outx0 < 0;
            var step = pitch1;
            step = (short)step;
            step = (step * factor) >> 15;
            step &= 0xFFFF;
            if (step > 0x3FFF)
            {
                step = 0x4000;
            }

            sawModulatedStep |= step != pitch1;
            counter1 += step;
            pushed1 += counter1 >> 12;
            counter1 &= 0xFFF;
        }

        Assert.True(sawNegativeFactor);
        Assert.True(sawModulatedStep);
        // Both SPUs have the same pitch modulation on voice 1, so the same current address after the run.
        Assert.Equal(reference.GetCurrentAddress(1), modulated.GetCurrentAddress(1));
    }

    [Fact]
    public void Pmon_VoiceOneAdvancesDifferentlyWithAndWithoutModulation()
    {
        var b = MakeBlock(10, 0, 7, 303);
        var plain = new PsxSpu(Tables());
        var modulated = new PsxSpu(Tables());
        foreach (var spu in new[] { plain, modulated })
        {
            spu.WriteRam(0x100 * 8, ToBytes(b));
            spu.SetStartAddress(0, 0x100);
            spu.SetStartAddress(1, 0x100);
            spu.SetPitch(0, 0x1000);
            spu.SetPitch(1, 0x1000);
            spu.SetAdsr(0, FullEnvelope);
            spu.SetAdsr(1, FullEnvelope);
            spu.SetVolume(0, false, MaxVolumeRegister);
            spu.SetVolume(1, false, MaxVolumeRegister);
        }

        modulated.SetPitchModulation(2);
        plain.KeyOn(3);
        modulated.KeyOn(3);
        var a = new short[2 * 400];
        var c = new short[2 * 400];
        plain.Render(a, 400);
        modulated.Render(c, 400);

        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Render_DoesNotAllocateWithAdsrNoiseAndPitchModulation()
    {
        var spu = new PsxSpu(Tables());
        var blocks = new[] { MakeBlock(9, 3, 4, 81), MakeBlock(9, 1, 0, 82), MakeBlock(8, 4, 3, 83) };
        var address = 0x100 * 8;
        foreach (var block in blocks)
        {
            spu.WriteRam(address, ToBytes(block));
            address += 16;
        }

        var words = new[]
        {
            Adsr(false, 3, 1, 4, 0x6, false, true, 9, 1, false, 8),
            Adsr(true, 8, 2, 6, 0x2, true, false, 10, 2, true, 10),
            FullEnvelope,
        };

        for (var v = 0; v < PsxSpu.VoiceCount; v++)
        {
            spu.SetStartAddress(v, 0x100);
            spu.SetPitch(v, (ushort)(0x0800 + v * 0x300));
            spu.SetAdsr(v, words[v % 3]);
            spu.SetVolume(v, false, 0x2000);
            spu.SetVolume(v, true, 0x2000);
        }

        spu.SetNoiseClock(7, 2);
        spu.SetNoiseMode(0x492492);
        spu.SetPitchModulation(0xAAAAAA);
        spu.KeyOn(0xFFFFFF);
        var buffer = new short[441 * 2];
        spu.Render(buffer, 441); // warm-up
        spu.KeyOff(0x0000F0);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            spu.Render(buffer, 441);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
