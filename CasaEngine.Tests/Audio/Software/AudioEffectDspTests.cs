using System.Diagnostics;
using CasaEngine.Framework.Audio.Effects;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// The DSP of the insert effects (plan T5.3, decision P20) driven directly: biquad coefficients and frequency response
/// against the formulas of the W3C "Audio EQ Cookbook" (https://www.w3.org/TR/audio-eq-cookbook/), compressor against
/// the equations of Giannoulis, Massberg and Reiss (JAES 2012), denormal guard.
/// </summary>
public class AudioEffectDspTests
{
    private const int Rate = 48000;

    // The cookbook, written again here in its published form (different structure from the engine code).
    private static (double B0, double B1, double B2, double A0, double A1, double A2) Cookbook(
        BiquadFilterType type, double f0, double q, double dbGain)
    {
        var a = Math.Pow(10, dbGain / 40);
        var w0 = 2 * Math.PI * f0 / Rate;
        var cos = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2 * q);
        var sqrtA2Alpha = 2 * Math.Sqrt(a) * alpha;

        return type switch
        {
            BiquadFilterType.LowPass => ((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha),
            BiquadFilterType.HighPass => ((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha),
            BiquadFilterType.BandPass => (alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha),
            BiquadFilterType.Peaking => (1 + (alpha * a), -2 * cos, 1 - (alpha * a), 1 + (alpha / a), -2 * cos, 1 - (alpha / a)),
            BiquadFilterType.LowShelf => (
                a * ((a + 1) - ((a - 1) * cos) + sqrtA2Alpha),
                2 * a * ((a - 1) - ((a + 1) * cos)),
                a * ((a + 1) - ((a - 1) * cos) - sqrtA2Alpha),
                (a + 1) + ((a - 1) * cos) + sqrtA2Alpha,
                -2 * ((a - 1) + ((a + 1) * cos)),
                (a + 1) + ((a - 1) * cos) - sqrtA2Alpha),
            _ => (
                a * ((a + 1) + ((a - 1) * cos) + sqrtA2Alpha),
                -2 * a * ((a - 1) + ((a + 1) * cos)),
                a * ((a + 1) + ((a - 1) * cos) - sqrtA2Alpha),
                (a + 1) - ((a - 1) * cos) + sqrtA2Alpha,
                2 * ((a - 1) - ((a + 1) * cos)),
                (a + 1) - ((a - 1) * cos) - sqrtA2Alpha),
        };
    }

    private static float[] Sine(double frequency, int frames, double amplitude = 0.5)
    {
        var buffer = new float[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            buffer[2 * i] = buffer[(2 * i) + 1] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / Rate));
        }

        return buffer;
    }

    // Amplitude of the left channel over the second half of the buffer (the transient is over).
    private static double SettledAmplitude(float[] buffer)
    {
        var peak = 0.0;

        for (var i = buffer.Length / 2; i < buffer.Length; i += 2)
        {
            peak = Math.Max(peak, Math.Abs(buffer[i]));
        }

        return peak;
    }

    private static double ResponseDb(BiquadFilterEffect effect, double frequency)
    {
        var state = new EffectDspState();
        var buffer = Sine(frequency, Rate);
        effect.Process(ref state, buffer, Rate, Rate);
        return 20 * Math.Log10(SettledAmplitude(buffer) / 0.5);
    }

    [Theory]
    [InlineData(BiquadFilterType.LowPass, 1000.0, 0.7071, 0.0)]
    [InlineData(BiquadFilterType.HighPass, 3000.0, 1.3, 0.0)]
    [InlineData(BiquadFilterType.BandPass, 440.0, 4.0, 0.0)]
    [InlineData(BiquadFilterType.Peaking, 2500.0, 1.0, 6.0)]
    [InlineData(BiquadFilterType.Peaking, 120.0, 2.5, -9.0)]
    [InlineData(BiquadFilterType.LowShelf, 200.0, 0.7071, 8.0)]
    [InlineData(BiquadFilterType.HighShelf, 8000.0, 0.9, -5.0)]
    public void TheCoefficients_MatchTheCookbookFormulas_NormalizedByA0(BiquadFilterType type, double frequency, double q, double gain)
    {
        var state = new EffectDspState();
        BiquadFilterEffect.ComputeCoefficients(type, (float)frequency, (float)q, (float)gain, Rate, ref state);

        var (b0, b1, b2, a0, a1, a2) = Cookbook(type, frequency, (float)q, (float)gain);
        Assert.Equal(b0 / a0, state.B0, 1e-12);
        Assert.Equal(b1 / a0, state.B1, 1e-12);
        Assert.Equal(b2 / a0, state.B2, 1e-12);
        Assert.Equal(a1 / a0, state.A1, 1e-12);
        Assert.Equal(a2 / a0, state.A2, 1e-12);
    }

    [Fact]
    public void ALowPassAtItsCutoff_IsThreeDecibelsDown()
    {
        var effect = new BiquadFilterEffect(BiquadFilterType.LowPass, 1000f, 0.70710678f);

        Assert.InRange(ResponseDb(effect, 1000), -3.5, -2.5);
        Assert.InRange(ResponseDb(effect, 100), -0.1, 0.1);
        Assert.True(ResponseDb(effect, 8000) < -30);
    }

    [Fact]
    public void AHighPassAtItsCutoff_IsThreeDecibelsDown_AndAPeakingFilterBoostsItsCentre()
    {
        var highPass = new BiquadFilterEffect(BiquadFilterType.HighPass, 1000f, 0.70710678f);
        Assert.InRange(ResponseDb(highPass, 1000), -3.5, -2.5);
        Assert.InRange(ResponseDb(highPass, 10000), -0.1, 0.1);

        var peaking = new BiquadFilterEffect(BiquadFilterType.Peaking, 2000f, 1f, 6f);
        Assert.InRange(ResponseDb(peaking, 2000), 5.9, 6.1);
        Assert.InRange(ResponseDb(peaking, 100), -0.3, 0.3);
    }

    [Fact]
    public void AParameterChange_IsPickedUpAtTheNextBlock_AndKeepsTheFilterMemory()
    {
        var effect = new BiquadFilterEffect(BiquadFilterType.LowPass, 1000f);
        var state = new EffectDspState();
        var buffer = Sine(1000, 480);
        effect.Process(ref state, buffer, 480, Rate);
        var memory = state.Z1Left;
        Assert.NotEqual(0.0, memory);

        effect.FrequencyHz = 4000f;
        effect.Process(ref state, new float[960], 480, Rate);

        var expected = new EffectDspState();
        BiquadFilterEffect.ComputeCoefficients(BiquadFilterType.LowPass, 4000f, 0.70710678f, 0f, Rate, ref expected);
        Assert.Equal(expected.B0, state.B0, 1e-15);
    }

    // Steady state gain of the compressor in dB on a constant input of the given level.
    private static double SteadyGainDb(CompressorEffect effect, double inputDb)
    {
        var state = new EffectDspState();
        var amplitude = (float)Math.Pow(10, inputDb / 20);
        var buffer = new float[2 * Rate]; // 1 s, many times the time constants below
        Array.Fill(buffer, amplitude);
        effect.Process(ref state, buffer, Rate, Rate);
        return 20 * Math.Log10(buffer[^2] / amplitude);
    }

    // Eq. (4) of the paper, written again.
    private static double Eq4(double x, double t, double r, double w)
    {
        if (2 * (x - t) < -w)
        {
            return x;
        }

        if (2 * (x - t) > w)
        {
            return t + ((x - t) / r);
        }

        return x + (((1 / r) - 1) * Math.Pow(x - t + (w / 2), 2) / (2 * w));
    }

    [Theory]
    [InlineData(-12.0)]
    [InlineData(-6.0)]
    [InlineData(-1.0)]
    public void TheSteadyStateGainReduction_MatchesTheStaticCharacteristic_HardKnee(double inputDb)
    {
        var effect = new CompressorEffect(-24f, 4f, 0f, 0.005f, 0.05f);

        var expected = Eq4(inputDb, -24, 4, 0) - inputDb; // y_G - x_G, negative
        Assert.Equal(expected, SteadyGainDb(effect, inputDb), 0.01);
    }

    [Theory]
    [InlineData(-30.0)]
    [InlineData(-20.0)]
    [InlineData(-14.0)]
    [InlineData(-5.0)]
    public void TheSteadyStateGainReduction_MatchesTheSoftKneeAndTheMakeupGain(double inputDb)
    {
        var effect = new CompressorEffect(-20f, 3f, 10f, 0.005f, 0.05f, 2f);

        var expected = Eq4(inputDb, -20, 3, 10) - inputDb + 2;
        Assert.Equal(expected, SteadyGainDb(effect, inputDb), 0.01);
    }

    [Fact]
    public void TheAttack_ReachesOneMinusOneOverEOfTheReduction_AfterOneTimeConstant()
    {
        const double attack = 0.05;
        var effect = new CompressorEffect(-24f, 4f, 0f, (float)attack, 0.2f);
        var state = new EffectDspState();
        var amplitude = (float)Math.Pow(10, -12.0 / 20);
        var buffer = new float[2 * Rate];
        Array.Fill(buffer, amplitude);

        effect.Process(ref state, buffer, Rate, Rate);

        // Target reduction (x_G - y_G) = 12 * 0.75 = 9 dB; one time constant in, the sample at index n has seen n + 1 steps.
        var index = (int)(attack * Rate) - 1;
        var reduction = -20 * Math.Log10(buffer[2 * index] / amplitude);
        Assert.InRange(reduction, 9 * (1 - Math.Exp(-1)) * 0.9, 9 * (1 - Math.Exp(-1)) * 1.1);
    }

    [Fact]
    public void TheRelease_FallsToOneOverEOfTheReduction_AfterOneTimeConstant()
    {
        const double release = 0.1;
        var effect = new CompressorEffect(-24f, 4f, 0f, 0.002f, (float)release);
        var state = new EffectDspState();
        var loud = new float[2 * Rate];
        Array.Fill(loud, (float)Math.Pow(10, -12.0 / 20));
        effect.Process(ref state, loud, Rate, Rate);
        Assert.Equal(9.0, state.GainReductionDb, 0.05);

        // Below the threshold: no more reduction wanted, so the gain climbs back along the release.
        var quietAmplitude = (float)Math.Pow(10, -40.0 / 20);
        var quiet = new float[2 * Rate];
        Array.Fill(quiet, quietAmplitude);
        effect.Process(ref state, quiet, Rate, Rate);

        var index = (int)(release * Rate) - 1;
        var reduction = -20 * Math.Log10(quiet[2 * index] / quietAmplitude);
        Assert.InRange(reduction, 9 / Math.E * 0.9, 9 / Math.E * 1.1);
    }

    [Fact]
    public void TheDetector_LinksTheTwoChannels()
    {
        var effect = new CompressorEffect(-24f, 4f, 0f, 0.0005f, 0.05f);
        var state = new EffectDspState();
        var buffer = new float[2 * Rate];

        for (var i = 0; i < buffer.Length; i += 2)
        {
            buffer[i] = 0.25f; // -12 dB
            buffer[i + 1] = 0.01f; // far below the threshold on its own
        }

        effect.Process(ref state, buffer, Rate, Rate);

        Assert.Equal(0.25 * Math.Pow(10, -9.0 / 20), buffer[^2], 1e-3);
        Assert.Equal(0.01 * Math.Pow(10, -9.0 / 20), buffer[^1], 1e-4);
    }

    [Fact]
    public void ALongDecayingTail_NeverReachesTheSubnormalRange()
    {
        var filter = new BiquadFilterEffect(BiquadFilterType.LowPass, 80f, 30f);
        var compressor = new CompressorEffect(-40f, 8f, 0f, 0.001f, 0.0001f);
        var filterState = new EffectDspState();
        var compressorState = new EffectDspState();
        var impulse = new float[2 * 480];
        impulse[0] = impulse[1] = 1f;
        filter.Process(ref filterState, impulse, 480, Rate);
        compressor.Process(ref compressorState, (float[])impulse.Clone(), 480, Rate);

        for (var block = 0; block < 2000; block++)
        {
            filter.Process(ref filterState, new float[2 * 480], 480, Rate);
            compressor.Process(ref compressorState, new float[2 * 480], 480, Rate);

            foreach (var value in new[] { filterState.Z1Left, filterState.Z2Left, filterState.Z1Right, filterState.Z2Right, compressorState.GainReductionDb })
            {
                Assert.True(value == 0.0 || Math.Abs(value) >= EffectDspState.DenormalFlush, $"block {block}: {value:E3} is below the guard");
            }
        }

        // The tail really died out (it was flushed, not just tiny).
        Assert.Equal(0.0, filterState.Z1Left);
        Assert.Equal(0.0, compressorState.GainReductionDb);
    }

    [Fact]
    public void RenderingADecayedTail_IsNotSlowerThanRenderingTheSignal()
    {
        var filter = new BiquadFilterEffect(BiquadFilterType.LowPass, 80f, 30f);
        var state = new EffectDspState();
        var noise = new float[2 * 480];
        var random = new Random(3);

        for (var i = 0; i < noise.Length; i++)
        {
            noise[i] = (float)((random.NextDouble() * 2) - 1);
        }

        var silence = new float[2 * 480];
        var work = new float[2 * 480];
        const int blocks = 400;

        // Wall-clock: the minimum over several runs, so that parallel test load (O20) only lengthens a run.
        double Run(float[] input)
        {
            var best = double.MaxValue;

            for (var trial = 0; trial < 7; trial++)
            {
                var watch = Stopwatch.StartNew();

                for (var block = 0; block < blocks; block++)
                {
                    Array.Copy(input, work, work.Length);
                    filter.Process(ref state, work, 480, Rate);
                }

                best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }

            return best;
        }

        var signal = Run(noise);

        // A tail with silent input after the noise: the memory decays through the whole range of doubles.
        for (var block = 0; block < 2000; block++)
        {
            filter.Process(ref state, new float[2 * 480], 480, Rate);
        }

        var tail = Run(silence);

        // Subnormal arithmetic would be 10 to 100 times slower; a generous 4x tolerates a loaded machine.
        Assert.True(tail < signal * 4 + 2, $"tail {tail:F2} ms against signal {signal:F2} ms");
    }
}
