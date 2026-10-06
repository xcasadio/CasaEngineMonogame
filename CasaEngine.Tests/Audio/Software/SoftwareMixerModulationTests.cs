using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Per-voice gain, spatial pan and speed ratio published as last values to <see cref="SoftwareMixer"/> (plan T9.1,
/// decision P32). The test thread plays both threads; expected values are recomputed from the pan laws and the formulas.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class SoftwareMixerModulationTests
{
    private const int OutputRate = 48000;
    private const int Block = 480;
    private const float FullScale = 32767f / 32768f;
    private const float Tolerance = 1e-5f;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static PcmAudioClip Constant(int frames, int channels = 1)
    {
        var samples = new short[frames * channels];
        Array.Fill(samples, short.MaxValue);
        return new PcmAudioClip(samples, OutputRate, channels);
    }

    private static PcmAudioClip Noise(int frames, int channels = 1, int seed = 1234)
    {
        var samples = new short[frames * channels];
        var random = new Random(seed);

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)random.Next(-20000, 20000);
        }

        return new PcmAudioClip(samples, OutputRate, channels);
    }

    // Mono voice with explicit gains of 1: the output is the sample value on both channels, times volume and modulation.
    private static void StartFlat(SoftwareMixer mixer, int slot, int generation, int frames = 400000)
    {
        Assert.True(mixer.TryStartResidentStereoVoice(slot, generation, Constant(frames), AudioVoiceParameters.Default, 1f, 1f));
    }

    private static float MonoLeft(float pan, float gain = 1f) => FullScale * gain * (float)Math.Cos((pan + 1.0) * Math.PI / 4.0);

    private static float MonoRight(float pan, float gain = 1f) => FullScale * gain * (float)Math.Sin((pan + 1.0) * Math.PI / 4.0);

    private static void AssertAll(float[] output, float left, float right, float tolerance = Tolerance)
    {
        for (var i = 0; i < output.Length; i += 2)
        {
            Assert.Equal(left, output[i], tolerance);
            Assert.Equal(right, output[i + 1], tolerance);
        }
    }

    private static int SoundingFrames(float[] output)
    {
        var count = 0;

        while (count < output.Length / 2 && output[count * 2] != 0f)
        {
            count++;
        }

        return count;
    }

    [Fact]
    public void TheInitialValuesTravelWithTheStart_TheFirstBlockIsAlreadyAtTheExpectedGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 0.5f, float.NaN, 1f);
        StartFlat(mixer, 0, 1);

        AssertAll(Render(mixer, Block), FullScale * 0.5f, FullScale * 0.5f);
    }

    [Fact]
    public void ALastValue_RampsLinearlyOverOneBlock_ThenStaysConstant()
    {
        var mixer = new SoftwareMixer(OutputRate);
        StartFlat(mixer, 0, 1);
        AssertAll(Render(mixer, 64), FullScale, FullScale);

        mixer.PublishVoiceModulation(0, 1, 0.5f, float.NaN, 1f);
        var ramp = Render(mixer, Block);

        for (var i = 0; i < Block; i++)
        {
            var expected = FullScale * (1f - 0.5f * (i + 1f) / Block);
            Assert.Equal(expected, ramp[i * 2], Tolerance);
            Assert.Equal(expected, ramp[i * 2 + 1], Tolerance);
        }

        AssertAll(Render(mixer, Block), FullScale * 0.5f, FullScale * 0.5f);
    }

    [Fact]
    public void TheGainComposesWithTheVolumeAndTheBusGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateBus(SoftwareMixer.MasterBus, out var bus));
        mixer.SetBusGain(bus, 0.5f);
        mixer.PublishVoiceModulation(0, 1, 0.5f, float.NaN, 1f);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(400000), AudioVoiceParameters.Default.WithVolume(0.8f), 1f, 1f, bus));
        Render(mixer, Block);

        AssertAll(Render(mixer, Block), FullScale * 0.8f * 0.5f * 0.5f, FullScale * 0.8f * 0.5f * 0.5f);
    }

    [Fact]
    public void AVoiceRampKeepsTheGain_SetVolumeEndsItWithoutTouchingTheGain_SetParametersKeepsTheGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 0.5f, float.NaN, 1f);
        StartFlat(mixer, 0, 1);
        Render(mixer, 64);

        const int frames = OutputRate / 4;
        Assert.True(mixer.TryRampVoiceVolume(0, 1, 0f, frames));
        var output = Render(mixer, 1000);

        for (var i = 0; i < 1000; i++)
        {
            var expected = 0.5f * FullScale * (1f - (i + 1f) / frames);
            Assert.Equal(expected, output[i * 2], Tolerance);
        }

        // The volume of SetParameters is ignored during the ramp, the gain stays.
        Assert.True(mixer.TrySetParameters(0, 1, AudioVoiceParameters.Default.WithVolume(0.9f)));
        output = Render(mixer, 100);
        Assert.Equal(0.5f * FullScale * (1f - 1001f / frames), output[0], Tolerance);

        // SetVolume ends the ramp: the volume is 0.9 and the gain is still 0.5.
        Assert.True(mixer.TrySetVolume(0, 1, 0.9f));
        Render(mixer, Block); // the volume goes from the ramp value to 0.9 across this block
        AssertAll(Render(mixer, Block), 0.9f * 0.5f * FullScale, 0.9f * 0.5f * FullScale);
    }

    [Fact]
    public void AValuePublishedForAnOlderGeneration_IsIgnoredByANewVoiceOfTheSameSlot()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 0.25f, 1f, 2f);
        StartFlat(mixer, 0, 2);

        AssertAll(Render(mixer, Block), FullScale, FullScale);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(0.37f)]
    public void ThePublishedPan_GivesTheMonoConstantPowerLaw(float pan)
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentVoice(0, 1, Constant(400000), AudioVoiceParameters.Default));
        Render(mixer, 64);

        mixer.PublishVoiceModulation(0, 1, 1f, pan, 1f);
        Render(mixer, Block);

        AssertAll(Render(mixer, Block), MonoLeft(pan), MonoRight(pan));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.4f)]
    public void ThePublishedPan_GivesTheStereoBalance(float pan)
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentVoice(0, 1, Constant(400000, 2), AudioVoiceParameters.Default));
        Render(mixer, 64);

        mixer.PublishVoiceModulation(0, 1, 1f, pan, 1f);
        Render(mixer, Block);

        var left = FullScale * (pan > 0f ? 1f - pan : 1f);
        var right = FullScale * (pan < 0f ? 1f + pan : 1f);
        AssertAll(Render(mixer, Block), left, right);
    }

    [Fact]
    public void ANanPan_RendersExactlyTheOwnPan_AndASetParametersAfterAPublishedPanDoesNotUndoIt()
    {
        var own = AudioVoiceParameters.Default.WithPan(0.3f);
        var reference = new SoftwareMixer(OutputRate);
        Assert.True(reference.TryStartResidentVoice(0, 1, Constant(400000), own));
        Render(reference, 3 * Block);
        var expected = Render(reference, Block);

        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentVoice(0, 1, Constant(400000), own));
        Render(mixer, Block);
        mixer.PublishVoiceModulation(0, 1, 1f, -1f, 1f);
        Render(mixer, Block);

        // A new own pan does not defeat the published one.
        Assert.True(mixer.TrySetParameters(0, 1, AudioVoiceParameters.Default.WithPan(0.9f)));
        Render(mixer, Block);
        AssertAll(Render(mixer, Block), MonoLeft(-1f), MonoRight(-1f));

        // NaN gives the own pan back (the last own pan received: 0.9).
        mixer.PublishVoiceModulation(0, 1, 1f, float.NaN, 1f);
        Render(mixer, Block);
        AssertAll(Render(mixer, Block), MonoLeft(0.9f), MonoRight(0.9f));

        // And a voice that never had a published pan renders the same bits as one that had NaN published.
        var viaNan = new SoftwareMixer(OutputRate);
        viaNan.PublishVoiceModulation(0, 1, 1f, float.NaN, 1f);
        Assert.True(viaNan.TryStartResidentVoice(0, 1, Constant(400000), own));
        Render(viaNan, 3 * Block);
        Assert.Equal(expected, Render(viaNan, Block));
    }

    [Fact]
    public void AVoiceWithExplicitGains_IgnoresThePan_AndKeepsTheGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 0.5f, 1f, 1f);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(400000), AudioVoiceParameters.Default, 0.5f, 0.25f));

        AssertAll(Render(mixer, Block), FullScale * 0.25f, FullScale * 0.125f);
    }

    [Fact]
    public void TheRate_MultipliesTheSpeedAndTheOtherFactors_AndIsBounded()
    {
        // 480 frames at rate 2 end after 240 frames.
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 1f, float.NaN, 2f);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(Block), AudioVoiceParameters.Default, 1f, 1f));
        Assert.Equal(Block / 2, SoundingFrames(Render(mixer, Block)));

        // Pitch +1 octave (x2) x multiplier 2 x rate 2 = 8: 60 frames.
        mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 1f, float.NaN, 2f);
        var parameters = AudioVoiceParameters.Default.WithPitch(1f).WithRateMultiplier(2f);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(Block), parameters, 1f, 1f));
        Assert.Equal(Block / 8, SoundingFrames(Render(mixer, Block)));

        // NaN and 0 mean 1; 100 is 16; a rate below 1/16 is 1/16.
        AssertSounding(float.NaN, Block);
        AssertSounding(0f, Block);
        AssertSounding(-3f, Block);
        AssertSounding(100f, Block / 16);
        AssertSounding(0.001f, Block);

        // The clip does not end at 1/16: its position is 30 frames after 480 output frames.
        var slow = new SoftwareMixer(OutputRate);
        slow.PublishVoiceModulation(0, 1, 1f, float.NaN, 0.001f);
        Assert.True(slow.TryStartResidentStereoVoice(0, 1, Constant(Block), AudioVoiceParameters.Default, 1f, 1f));
        Render(slow, Block);
        Assert.False(slow.TryDequeueEvent(out _));
    }

    private static void AssertSounding(float rate, int expectedFrames)
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 1f, float.NaN, rate);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(Block), AudioVoiceParameters.Default, 1f, 1f));
        Assert.Equal(expectedFrames, SoundingFrames(Render(mixer, Block)));
    }

    [Fact]
    public void ARateChangedAfterTheStart_KeepsThePitch()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var parameters = AudioVoiceParameters.Default.WithPitch(1f);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, Constant(2 * Block), parameters, 1f, 1f));
        Assert.Equal(Block / 4, SoundingFrames(Render(mixer, Block / 4))); // step 2: 120 frames, position 240
        mixer.PublishVoiceModulation(0, 1, 1f, float.NaN, 2f);

        // Step 2 (pitch) x 2 (rate) = 4: the 720 frames left end after 180 frames.
        Assert.Equal(180, SoundingFrames(Render(mixer, Block)));
    }

    [Fact]
    public void APausedVoice_AppliesItsGainAtTheResume_WithoutAJump()
    {
        var mixer = new SoftwareMixer(OutputRate);
        StartFlat(mixer, 0, 1);
        Render(mixer, 64);

        Assert.True(mixer.TryPause(0, 1));
        mixer.PublishVoiceModulation(0, 1, 0.5f, float.NaN, 1f);
        Assert.All(Render(mixer, 3 * Block), sample => Assert.Equal(0f, sample));

        Assert.True(mixer.TryResume(0, 1));
        var output = Render(mixer, Block);

        for (var i = 0; i < Block; i++)
        {
            Assert.Equal(FullScale * (1f - 0.5f * (i + 1f) / Block), output[i * 2], Tolerance);
        }

        AssertAll(Render(mixer, Block), FullScale * 0.5f, FullScale * 0.5f);
    }

    private static byte[] ConstantPcm(int frames)
    {
        var pcm = new byte[frames * 2];

        for (var i = 0; i < frames; i++)
        {
            pcm[i * 2] = 0xFF;
            pcm[i * 2 + 1] = 0x7F;
        }

        return pcm;
    }

    private static SoftwareMixer StreamingMixer(int buffers, int framesPerBuffer, int generation = 1)
    {
        var mixer = new SoftwareMixer(OutputRate, voiceCapacity: 2);
        Assert.True(mixer.TryCreateStreamingVoice(0, generation, 1, OutputRate, AudioVoiceParameters.Default));
        var pcm = ConstantPcm(framesPerBuffer);

        for (var i = 0; i < buffers; i++)
        {
            Assert.True(mixer.TrySubmitStreamingBuffer(0, generation, pcm, i));
        }

        return mixer;
    }

    [Fact]
    public void AStreamingVoice_AppliesGainPanAndRate()
    {
        // Gain and pan, from the creation.
        var mixer = new SoftwareMixer(OutputRate, voiceCapacity: 2);
        mixer.PublishVoiceModulation(0, 1, 0.5f, 1f, 1f);
        Assert.True(mixer.TryCreateStreamingVoice(0, 1, 1, OutputRate, AudioVoiceParameters.Default));
        Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, ConstantPcm(2000), 0));
        Assert.True(mixer.TryStartStreamingVoice(0, 1));
        AssertAll(Render(mixer, Block), MonoLeft(1f, 0.5f), MonoRight(1f, 0.5f));

        // A last value on a running stream: gain ramps then holds.
        mixer.PublishVoiceModulation(0, 1, 0.25f, 0f, 1f);
        Render(mixer, Block);
        AssertAll(Render(mixer, Block), MonoLeft(0f, 0.25f), MonoRight(0f, 0.25f));

        // Rate 2 consumes the buffers twice as fast.
        var normal = StreamingMixer(6, Block);
        Assert.True(normal.TryStartStreamingVoice(0, 1));
        Render(normal, Block);
        var fast = StreamingMixer(6, Block);
        fast.PublishVoiceModulation(0, 1, 1f, float.NaN, 2f);
        Assert.True(fast.TryStartStreamingVoice(0, 1));
        Render(fast, Block);

        Assert.True(fast.GetConsumedBufferCount(0, 1) >= 2 * normal.GetConsumedBufferCount(0, 1) - 1);
        Assert.True(fast.GetConsumedBufferCount(0, 1) > normal.GetConsumedBufferCount(0, 1));
    }

    [Fact]
    public void AValuePublishedBetweenTheCreationAndTheStart_IsTheStartingValueOfAStreamingVoice()
    {
        var mixer = StreamingMixer(8, Block);
        Render(mixer, Block); // created, not started: renders nothing and applies nothing
        mixer.PublishVoiceModulation(0, 1, 0.25f, float.NaN, 2f);
        Assert.True(mixer.TryStartStreamingVoice(0, 1));

        // From the first rendered block: 0.25 on every sample (no ramp from 1) and double speed.
        AssertAll(Render(mixer, Block), MonoLeft(0f, 0.25f), MonoRight(0f, 0.25f));
        Assert.True(mixer.GetConsumedBufferCount(0, 1) >= 2);
    }

    [Fact]
    public void AVoiceStartedWithoutAnyPublication_IsAtFullGain_AndItsSpatialFieldsAreSet()
    {
        var mixer = new SoftwareMixer(OutputRate);
        // A slot that had a gain-0 voice before: nothing published for the new voice's generation.
        mixer.PublishVoiceModulation(0, 1, 0f, 1f, 4f);
        StartFlat(mixer, 0, 1);
        Assert.True(mixer.TryStop(0, 1));
        Render(mixer, 64);

        Assert.True(mixer.TryStartResidentVoice(0, 7, Constant(400000), AudioVoiceParameters.Default));
        AssertAll(Render(mixer, Block), MonoLeft(0f), MonoRight(0f));
    }

    private static void PlayScenario(SoftwareMixer mixer, bool publishUnity, List<float[]> blocks)
    {
        void Publish(int slot, int generation)
        {
            if (publishUnity)
            {
                mixer.PublishVoiceModulation(slot, generation, 1f, float.NaN, 1f);
            }
        }

        Publish(0, 1);
        Assert.True(mixer.TryStartResidentVoice(0, 1, Noise(100000), AudioVoiceParameters.Default.WithPan(0.3f).WithPitch(0.2f).WithRateMultiplier(1.5f).WithLooping(true)));
        Publish(1, 1);
        Assert.True(mixer.TryStartResidentVoice(1, 1, Noise(100000, 2, 99), AudioVoiceParameters.Default.WithPan(-0.6f).WithVolume(0.7f)));
        Publish(2, 1);
        Assert.True(mixer.TryStartResidentStereoVoice(2, 1, Noise(100000, 1, 5), AudioVoiceParameters.Default, 0.9f, 0.4f));
        Publish(3, 1);
        Assert.True(mixer.TryCreateStreamingVoice(3, 1, 1, OutputRate, AudioVoiceParameters.Default.WithPan(0.2f)));
        Assert.True(mixer.TrySubmitStreamingBuffer(3, 1, new byte[8000], 0));
        Assert.True(mixer.TryStartStreamingVoice(3, 1));

        for (var block = 0; block < 40; block++)
        {
            if (block == 5)
            {
                Assert.True(mixer.TryRampVoiceVolume(0, 1, 0.2f, 3000));
            }

            if (block == 12)
            {
                Assert.True(mixer.TryPause(1, 1));
            }

            if (block == 18)
            {
                Assert.True(mixer.TryResume(1, 1));
                Assert.True(mixer.TrySetVolume(0, 1, 0.9f));
            }

            if (block == 25)
            {
                Assert.True(mixer.TrySetParameters(2, 1, AudioVoiceParameters.Default.WithVolume(0.5f)));
            }

            if (publishUnity)
            {
                for (var slot = 0; slot < 4; slot++)
                {
                    mixer.PublishVoiceModulation(slot, 1, 1f, float.NaN, 1f);
                }
            }

            blocks.Add(Render(mixer, 480));
        }
    }

    [Fact]
    public void WithoutAnyPublication_TheOutputIsBitForBitTheOneOfPublishedUnityValues()
    {
        var plain = new List<float[]>();
        var unity = new List<float[]>();
        PlayScenario(new SoftwareMixer(OutputRate), false, plain);
        PlayScenario(new SoftwareMixer(OutputRate), true, unity);

        Assert.Equal(plain.Count, unity.Count);
        var peak = 0f;

        for (var b = 0; b < plain.Count; b++)
        {
            Assert.True(plain[b].AsSpan().SequenceEqual(unity[b]), $"block {b} differs");

            foreach (var sample in plain[b])
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }
        }

        Assert.True(peak > 0.1f);
    }

    [Fact]
    public void ChangingTheThreeValuesEveryBlockOn64Voices_AllocatesNothing()
    {
        var mixer = new SoftwareMixer(OutputRate, voiceCapacity: 64);
        var clip = Noise(100000);

        for (var slot = 0; slot < 64; slot++)
        {
            Assert.True(mixer.TryStartResidentVoice(slot, 1, clip, AudioVoiceParameters.Default.WithLooping(true)));
        }

        var buffer = new float[Block * 2];
        var smallest = long.MaxValue;

        for (var round = 0; round < 3 && smallest != 0; round++)
        {
            // Warm up.
            for (var block = 0; block < 4; block++)
            {
                PublishAndRender(mixer, buffer, block);
            }

            var before = AllocationWindow.Start();

            for (var block = 0; block < 100; block++)
            {
                PublishAndRender(mixer, buffer, block);
            }

            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, smallest);
    }

    private static void PublishAndRender(SoftwareMixer mixer, float[] buffer, int block)
    {
        for (var slot = 0; slot < 64; slot++)
        {
            var phase = (block + slot) % 8;
            mixer.PublishVoiceModulation(slot, 1, 0.1f + 0.1f * phase, -1f + 0.25f * phase, 0.5f + 0.25f * phase);
        }

        mixer.Render(buffer, Block);
    }

    [Fact]
    public void AVoiceWaitingForItsStop_KeepsItsModulation_WhenItsSlotIsPublishedForANewerVoice()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.PublishVoiceModulation(0, 1, 0.05f, 0.9f, 1f);
        Assert.True(mixer.TryStartResidentVoice(0, 1, Constant(400000), AudioVoiceParameters.Default));
        AssertAll(Render(mixer, Block), MonoLeft(0.9f, 0.05f), MonoRight(0.9f, 0.05f));

        // The backend reuses a slot as soon as the Stop of its voice is queued, and publishes the values of the next
        // voice before the mixer has applied that Stop: the old voice renders this block with its own values.
        mixer.PublishVoiceModulation(0, 2, 1f, float.NaN, 1f);

        AssertAll(Render(mixer, Block), MonoLeft(0.9f, 0.05f), MonoRight(0.9f, 0.05f));
    }

    [Fact]
    public void AStreamingVoiceChangedBeforeItsStart_WithNothingPublished_RampsItsFirstBlockAsBefore()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryCreateStreamingVoice(0, 1, 1, OutputRate, AudioVoiceParameters.Default));
        Assert.True(mixer.TrySetVolume(0, 1, 0.3f));
        Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, ConstantPcm(4000), 0));
        Assert.True(mixer.TryStartStreamingVoice(0, 1));

        var first = Render(mixer, Block);
        var second = Render(mixer, Block);

        // The channel gains go from the creation volume to the new volume over the first block, as they did before the
        // modulation channel existed: the start does not snap them.
        Assert.True(first[0] > MonoLeft(0f, 0.3f) + 0.3f, $"first frame {first[0]}");
        Assert.Equal(MonoLeft(0f, 0.3f), first[(Block - 1) * 2], 1e-3f);
        Assert.Equal(MonoLeft(0f, 0.3f), second[0], 1e-3f);
        Assert.Equal(MonoLeft(0f, 0.3f), second[(Block - 1) * 2], 1e-3f);
    }
}
