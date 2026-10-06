using CasaEngine.Framework.Audio.Effects;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// The DSP of <see cref="ReverbEffect"/> (Freeverb, from the CCRMA description by J. O. Smith III) and
/// <see cref="LimiterEffect"/> (compressor design of Giannoulis, Massberg and Reiss, JAES 2012) driven directly (plan T5.4, P20).
/// </summary>
public class ReverbLimiterDspTests
{
    private static readonly int[] CombsAt44100 = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
    private static readonly int[] AllpassesAt44100 = { 225, 556, 441, 341 };

    // The scaling written again from the description: length at 44.1 kHz times rate / 44100, nearest sample.
    private static int Expected(int at44100, int rate)
    {
        return (int)Math.Round(at44100 * (double)rate / 44100.0, MidpointRounding.AwayFromZero);
    }

    private static float[] ImpulseResponse(ReverbEffect effect, int rate, int frames)
    {
        var state = new EffectDspState { Extra = effect.CreateAudioState(rate) };
        var buffer = new float[frames * 2];
        buffer[0] = 1f;
        buffer[1] = 1f;

        // Several blocks of an odd size, like a real render.
        for (var done = 0; done < frames; done += 333)
        {
            var count = Math.Min(333, frames - done);
            effect.Process(ref state, buffer.AsSpan(done * 2, count * 2), count, rate);
        }

        return buffer;
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    [InlineData(22050)]
    public void TheDelayLengths_AreTheTuningsOfThePageScaledToTheOutputRate(int rate)
    {
        var state = (ReverbEffect.ReverbState)new ReverbEffect().CreateAudioState(rate);

        for (var i = 0; i < ReverbEffect.CombCount; i++)
        {
            Assert.Equal(Expected(CombsAt44100[i], rate), ReverbEffect.GetCombLength(i, false, rate));
            Assert.Equal(Expected(CombsAt44100[i] + 23, rate), ReverbEffect.GetCombLength(i, true, rate));
            Assert.Equal(ReverbEffect.GetCombLength(i, false, rate), state.CombBuffers[i].Length);
            Assert.Equal(ReverbEffect.GetCombLength(i, true, rate), state.CombBuffers[ReverbEffect.CombCount + i].Length);
        }

        for (var i = 0; i < ReverbEffect.AllpassCount; i++)
        {
            Assert.Equal(Expected(AllpassesAt44100[i], rate), ReverbEffect.GetAllpassLength(i, false, rate));
            Assert.Equal(Expected(AllpassesAt44100[i] + 23, rate), ReverbEffect.GetAllpassLength(i, true, rate));
            Assert.Equal(ReverbEffect.GetAllpassLength(i, false, rate), state.AllpassBuffers[i].Length);
            Assert.Equal(ReverbEffect.GetAllpassLength(i, true, rate), state.AllpassBuffers[ReverbEffect.AllpassCount + i].Length);
        }

        Assert.Equal(1557, ReverbEffect.GetCombLength(0, false, 44100));
        Assert.Equal(1580, ReverbEffect.GetCombLength(0, true, 44100));
        Assert.Equal(1695, ReverbEffect.GetCombLength(0, false, 48000));
        Assert.Equal(1, ReverbEffect.ScaleDelay(1, 8000));
    }

    [Fact]
    public void TheImpulseResponse_StartsAtTheShortestCombDelay_OnEachChannel()
    {
        const int rate = 48000;
        var response = ImpulseResponse(new ReverbEffect(0.8f), rate, rate);

        // Separation 1: the left output comes from the left lines only, the right output from the right lines.
        var firstLeft = Array.FindIndex(Enumerable.Range(0, rate).ToArray(), n => response[n * 2] != 0f);
        var firstRight = Array.FindIndex(Enumerable.Range(0, rate).ToArray(), n => response[(n * 2) + 1] != 0f);

        Assert.Equal(Expected(CombsAt44100.Min(), rate), firstLeft);
        Assert.Equal(Expected(CombsAt44100.Min() + 23, rate), firstRight);
    }

    [Fact]
    public void TheImpulseResponse_DecaysWithoutNonFiniteValues_OrSubnormalOnes()
    {
        const int rate = 48000;
        const int seconds = 40;
        var response = ImpulseResponse(new ReverbEffect(0.9f), rate, seconds * rate);

        Assert.All(response, sample => Assert.True(float.IsFinite(sample)));

        // Energy per half second window: after the build-up of the echoes (second window on) it only decreases.
        var window = rate / 2;
        var energies = new List<double>();

        for (var start = 0; start + window <= seconds * rate; start += window)
        {
            double energy = 0;

            for (var n = start * 2; n < (start + window) * 2; n++)
            {
                energy += (double)response[n] * response[n];
            }

            energies.Add(energy);
        }

        Assert.True(energies[1] > 0);

        for (var i = 2; i < 20; i++)
        {
            Assert.True(energies[i] < energies[i - 1], $"window {i}: {energies[i]} is not under {energies[i - 1]}");
        }

        // The tail is far under audibility, and the denormal guard keeps it out of the subnormal range.
        Assert.True(energies[^1] < 1e-20);
        Assert.All(response, sample => Assert.True(sample == 0f || float.IsNormal(sample)));
    }

    [Fact]
    public void ADryOnlyReverb_PassesTheInput_AndAMonoSeparationGivesTheSameOnBothChannels()
    {
        const int rate = 48000;
        var dry = new ReverbEffect(wet: 0f, dry: 1f);
        var state = new EffectDspState { Extra = dry.CreateAudioState(rate) };
        var buffer = new float[200];

        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (i % 7) * 0.1f;
        }

        var copy = (float[])buffer.Clone();
        dry.Process(ref state, buffer, 100, rate);
        Assert.Equal(copy, buffer);

        var mono = ImpulseResponse(new ReverbEffect(0.8f, stereoSeparation: 0f), rate, rate / 2);

        for (var n = 0; n < rate / 2; n++)
        {
            Assert.Equal(mono[n * 2], mono[(n * 2) + 1]);
        }
    }

    [Fact]
    public void ReverbParameters_AreClampedAndNanIsIgnored()
    {
        var effect = new ReverbEffect(roomSize: 5f, damping: float.NaN, wet: -1f);
        Assert.Equal(1f, effect.RoomSize);
        Assert.Equal(0.5f, effect.Damping);
        Assert.Equal(0f, effect.Wet);
        effect.Damping = float.NaN;
        Assert.Equal(0.5f, effect.Damping);
    }

    [Fact]
    public void TheLimiter_HoldsASteadyOvershootAtTheCeiling_AndLeavesAQuietSignalUntouched()
    {
        const int rate = 48000;
        var limiter = new LimiterEffect();
        var state = new EffectDspState();
        var loud = new float[rate / 5 * 2];
        Array.Fill(loud, 4f);
        limiter.Process(ref state, loud, loud.Length / 2, rate);

        Assert.Equal(Math.Pow(10, -1.0 / 20.0), loud[^2], 1e-3);
        Assert.Equal(loud[^2], loud[^1]);

        var quiet = new EffectDspState();
        var soft = new float[960];
        Array.Fill(soft, 0.5f);
        new LimiterEffect().Process(ref quiet, soft, 480, rate);
        Assert.All(soft, sample => Assert.Equal(0.5f, sample));
    }

    [Fact]
    public void TheLimiter_FollowsItsCeilingAndCanBeDisabled()
    {
        const int rate = 48000;
        var limiter = new LimiterEffect { CeilingDb = -6f };
        var state = new EffectDspState();
        var loud = new float[rate / 5 * 2];
        Array.Fill(loud, 1f);
        limiter.Process(ref state, loud, loud.Length / 2, rate);
        Assert.Equal(Math.Pow(10, -6.0 / 20.0), loud[^2], 1e-3);

        limiter.IsEnabled = false;
        var passed = new float[960];
        Array.Fill(passed, 3f);
        limiter.Process(ref state, passed, 480, rate);
        Assert.All(passed, sample => Assert.Equal(3f, sample));

        limiter.CeilingDb = float.NaN;
        Assert.Equal(-6f, limiter.CeilingDb);
    }

    [Fact]
    public void TheLimiterAttack_ReachesTheReductionWithinItsTimeConstants_AndReleasesSlowly()
    {
        const int rate = 48000;
        var limiter = new LimiterEffect(-1f, 0.001f, 0.1f);
        var state = new EffectDspState();
        var block = new float[rate / 2 * 2];
        Array.Fill(block, 2f);
        var needed = (20 * Math.Log10(2.0)) + 1.0;

        // After one attack constant (1 ms) the smoothed reduction has covered 1 - 1/e of the step.
        limiter.Process(ref state, block.AsSpan(0, (rate / 1000) * 2), rate / 1000, rate);
        Assert.Equal(needed * (1 - Math.Exp(-1)), state.GainReductionDb, needed * 0.05);

        // Release: input falls well under the ceiling, the reduction decays with the 100 ms constant.
        limiter.Process(ref state, block.AsSpan(0, rate / 2 * 2), rate / 2, rate);
        Assert.Equal(needed, state.GainReductionDb, needed * 0.01);
        var quiet = new float[rate / 10 * 2];
        Array.Fill(quiet, 0.1f);
        limiter.Process(ref state, quiet, quiet.Length / 2, rate);
        Assert.Equal(needed * Math.Exp(-1), state.GainReductionDb, needed * 0.05);
    }
}
