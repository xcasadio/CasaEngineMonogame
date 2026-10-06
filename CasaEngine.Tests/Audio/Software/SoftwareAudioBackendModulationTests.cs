using System.Diagnostics;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// <see cref="IAudioVoiceModulationBackend"/> on <see cref="SoftwareAudioBackend"/> (plan T9.1, decision P32), driven through
/// an offline output: one <c>Pump</c> is one audio block (480 frames at 48000 Hz).
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class SoftwareAudioBackendModulationTests
{
    private const int Block = 480;
    private const float FullScale = 32767f / 32768f;
    private const float Tolerance = 1e-5f;

    private static SoftwareAudioBackend CreateBackend(out OfflineAudioOutput output, int voiceCapacity = 8)
    {
        output = new OfflineAudioOutput();
        return new SoftwareAudioBackend(output, voiceCapacity);
    }

    private static PcmAudioClip Constant(int frames = 100000)
    {
        var samples = new short[frames];
        Array.Fill(samples, short.MaxValue);
        return new PcmAudioClip(samples, 48000, 1);
    }

    private static float Left(float gain = 1f, float pan = 0f) => FullScale * gain * (float)Math.Cos((pan + 1.0) * Math.PI / 4.0);

    private static float Right(float gain = 1f, float pan = 0f) => FullScale * gain * (float)Math.Sin((pan + 1.0) * Math.PI / 4.0);

    private static void AssertBlock(OfflineAudioOutput output, float left, float right)
    {
        var block = output.LastBlock;

        for (var i = 0; i < block.Length; i += 2)
        {
            Assert.Equal(left, block[i], Tolerance);
            Assert.Equal(right, block[i + 1], Tolerance);
        }
    }

    [Fact]
    public void TheCapabilityIsOnlyImplementedByTheSoftwareBackend()
    {
        Assert.True(typeof(IAudioVoiceModulationBackend).IsAssignableFrom(typeof(SoftwareAudioBackend)));
        Assert.False(typeof(IAudioVoiceModulationBackend).IsAssignableFrom(typeof(NullAudioBackend)));
        Assert.False(typeof(IAudioVoiceModulationBackend).IsAssignableFrom(typeof(MonoGameAudioBackend)));
        Assert.False(typeof(IAudioVoiceModulationBackend).IsAssignableFrom(typeof(FakeAudioBackend)));
    }

    [Fact]
    public void TheNextVoiceValues_TravelWithTheStartOfThatVoiceAlone()
    {
        using var backend = CreateBackend(out var output);
        var modulation = (IAudioVoiceModulationBackend)backend;
        using var clip = Constant();

        modulation.SetNextVoiceModulation(0.5f, 1f, 1f);
        var first = backend.Play(clip, AudioVoiceParameters.Default.WithVolume(0.4f));
        var second = backend.Play(clip, AudioVoiceParameters.Default.WithVolume(0.4f));

        output.Pump(Block);
        output.Pump(Block);

        // Voice 1: gain 0.5, hard right; voice 2: untouched (gain 1, centre): the sum of both is checked per channel.
        AssertBlock(output, 0.4f * (Left(0.5f, 1f) + Left()), 0.4f * (Right(0.5f, 1f) + Right()));
        Assert.True(first.IsValid && second.IsValid);
    }

    [Fact]
    public void TheNextVoiceValues_AreConsumedEvenWhenThePlayIsRefused()
    {
        using var backend = CreateBackend(out var output);
        var modulation = (IAudioVoiceModulationBackend)backend;
        using var refused = Constant(16);
        refused.Dispose();
        using var clip = Constant();

        modulation.SetNextVoiceModulation(0f, 1f, 4f);
        Assert.Equal(AudioVoiceHandle.None, backend.Play(refused, AudioVoiceParameters.Default));

        backend.Play(clip, AudioVoiceParameters.Default);
        output.Pump(Block);
        output.Pump(Block);

        AssertBlock(output, Left(), Right());
    }

    [Fact]
    public void TheNextVoiceValues_AreConsumedEvenWhenNoVoiceIsFree()
    {
        using var backend = CreateBackend(out var output, voiceCapacity: 1);
        var modulation = (IAudioVoiceModulationBackend)backend;
        using var clip = Constant();
        var holder = backend.Play(clip, AudioVoiceParameters.Default);
        Assert.True(holder.IsValid);

        modulation.SetNextVoiceModulation(0f, 1f, 1f);
        Assert.Equal(AudioVoiceHandle.None, backend.Play(clip, AudioVoiceParameters.Default));

        backend.Release(holder);
        output.Pump(Block);
        var next = backend.Play(clip, AudioVoiceParameters.Default);
        Assert.True(next.IsValid);
        output.Pump(Block);
        output.Pump(Block);

        AssertBlock(output, Left(), Right());
    }

    [Fact]
    public void ALastValue_IsAppliedToALiveVoice_AndAStaleHandleIsIgnored()
    {
        using var backend = CreateBackend(out var output);
        var modulation = (IAudioVoiceModulationBackend)backend;
        using var clip = Constant();

        var old = backend.Play(clip, AudioVoiceParameters.Default);
        backend.Release(old);
        output.Pump(Block);

        var voice = backend.Play(clip, AudioVoiceParameters.Default);
        modulation.SetVoiceModulation(old, 0f, 1f, 1f); // stale: ignored
        output.Pump(Block);
        output.Pump(Block);
        AssertBlock(output, Left(), Right());

        modulation.SetVoiceModulation(voice, 0.5f, -1f, 1f);
        output.Pump(Block);
        output.Pump(Block);
        AssertBlock(output, Left(0.5f, -1f), Right(0.5f, -1f));

        // A handle that never existed, or an invalid one: nothing happens.
        modulation.SetVoiceModulation(AudioVoiceHandle.None, 0f, 1f, 1f);
        modulation.SetVoiceModulation(new AudioVoiceHandle(7, 3), 0f, 1f, 1f);
        output.Pump(Block);
        AssertBlock(output, Left(0.5f, -1f), Right(0.5f, -1f));
    }

    [Fact]
    public void AStreamingVoice_GetsItsNextVoiceValuesAndALaterLastValue()
    {
        using var backend = CreateBackend(out var output);
        var modulation = (IAudioVoiceModulationBackend)backend;
        var pcm = new byte[2 * 20000];

        for (var i = 0; i < 20000; i++)
        {
            pcm[i * 2] = 0xFF;
            pcm[i * 2 + 1] = 0x7F;
        }

        modulation.SetNextVoiceModulation(0.5f, float.NaN, 1f);
        var voice = backend.CreateStreamingVoice(48000, 1, AudioVoiceParameters.Default);
        Assert.True(voice.IsValid);
        backend.SubmitBuffer(voice, pcm, 0, pcm.Length);
        backend.Start(voice);
        output.Pump(Block);
        output.Pump(Block);
        AssertBlock(output, Left(0.5f), Right(0.5f));

        // Set between the creation and the start: it is the starting value.
        modulation.SetNextVoiceModulation(1f, float.NaN, 1f);
        var second = backend.CreateStreamingVoice(48000, 1, AudioVoiceParameters.Default);
        modulation.SetVoiceModulation(second, 0.25f, float.NaN, 1f);
        backend.SubmitBuffer(second, pcm, 0, pcm.Length);
        backend.Stop(voice);
        backend.Start(second);
        output.Pump(Block);
        AssertBlock(output, Left(0.25f), Right(0.25f));
    }

    [Fact]
    public void SetVoiceModulation_NeverWaits_OnASaturatedCommandRing()
    {
        using var backend = CreateBackend(out var output);
        var modulation = (IAudioVoiceModulationBackend)backend;
        using var clip = Constant();
        var voice = backend.Play(clip, AudioVoiceParameters.Default);
        output.Pump(Block);

        // Nothing pumps the output: the ring fills up (SetVolume is not retried, so this loop does not wait either).
        for (var i = 0; i < 6000; i++)
        {
            backend.SetVolume(voice, 1f);
        }

        var watch = Stopwatch.StartNew();

        for (var i = 0; i < 100; i++)
        {
            modulation.SetVoiceModulation(voice, 0.5f, float.NaN, 1f);
        }

        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 50, $"100 calls took {watch.ElapsedMilliseconds} ms");

        output.Pump(Block);
        output.Pump(Block);
        AssertBlock(output, Left(0.5f), Right(0.5f));
    }
}
