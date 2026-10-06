using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Ducking on <see cref="SoftwareMixer"/> (plan T5.5, decision P20): the target bus attenuated by the configured depth while the
/// source is above the threshold, back after the release, the source mixed before the target in the same block whatever the
/// creation order, cycles refused, no allocation.
/// </summary>
public class SoftwareMixerDuckingTests
{
    private const int OutputRate = 48000;
    private const float MusicValue = 0.5f;
    private const float DialogueValue = 0.1f;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static float Last(float[] block)
    {
        return block[^2];
    }

    private static int CreateBus(SoftwareMixer mixer, int parent = SoftwareMixer.MasterBus)
    {
        Assert.True(mixer.TryCreateBus(parent, out var bus));
        return bus;
    }

    private static void StartConstantVoice(SoftwareMixer mixer, int slot, int bus, float value)
    {
        var samples = new short[OutputRate * 20];
        Array.Fill(samples, (short)Math.Round(value * 32768f));
        Assert.True(mixer.TryStartResidentStereoVoice(slot, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default, 1f, 1f, bus));
    }

    // The effect needs a game-side bus for its source; the mixer only uses the index it is given.
    private static DuckingEffect NewDucking(float depthDb = 12f, float thresholdDb = -40f, float attackSeconds = 0.005f, float releaseSeconds = 0.1f)
    {
        var scratch = new AudioMixer();
        return new DuckingEffect(scratch.CreateBus("Source", null), depthDb, thresholdDb, attackSeconds, releaseSeconds);
    }

    private static float Gain(float depthDb)
    {
        return (float)Math.Pow(10.0, -depthDb / 20.0);
    }

    [Fact]
    public void WhileTheSourceIsActive_TheTargetIsAttenuatedByTheDepth_AndComesBackAfterTheRelease()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var music = CreateBus(mixer);
        var dialogue = CreateBus(mixer);
        StartConstantVoice(mixer, 0, music, MusicValue);
        Assert.True(mixer.TryAddDuckingEffect(music, NewDucking(), dialogue));

        // Source silent: nothing to duck.
        for (var i = 0; i < 10; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal(MusicValue, Last(Render(mixer, 480)), 1e-4f);

        // Dialogue starts: 0.5 s of attack (100 time constants) is the full depth.
        StartConstantVoice(mixer, 1, dialogue, DialogueValue);

        for (var i = 0; i < 50; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal((MusicValue * Gain(12f)) + DialogueValue, Last(Render(mixer, 480)), 2e-3f);

        // Dialogue stops: the follower falls under the threshold after about 115 ms, then the release (100 ms) brings the music back.
        Assert.True(mixer.TryStop(1, 1));
        float halfWay = 0f;

        for (var i = 0; i < 100; i++)
        {
            var block = Render(mixer, 480);

            if (i == 23)
            {
                halfWay = Last(block);
            }
        }

        // About 0.24 s after the stop, 0.125 s after the release began: the attenuation of 12 dB is down to about 3.4 dB.
        Assert.InRange(halfWay, (MusicValue * Gain(12f)) + 0.02f, MusicValue - 0.02f);
        Assert.Equal(MusicValue, Last(Render(mixer, 480)), 2e-3f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSourceIsMixedBeforeTheTarget_InTheSameBlock_WhateverTheCreationOrder(bool sourceCreatedFirst)
    {
        var mixer = new SoftwareMixer(OutputRate);
        int music;
        int dialogue;
        int dialogueChild;

        // The dialogue is on a child of the source bus: the source buffer is only complete once that child was mixed into it, so the
        // source has to come after its children and before the target (voices alone would not tell, they are mixed before any bus).
        // Created first, the child has a lower index than the target and the plain "highest index first" order would mix the
        // target first.
        if (sourceCreatedFirst)
        {
            dialogue = CreateBus(mixer);
            dialogueChild = CreateBus(mixer, dialogue);
            music = CreateBus(mixer);
        }
        else
        {
            music = CreateBus(mixer);
            dialogue = CreateBus(mixer);
            dialogueChild = CreateBus(mixer, dialogue);
        }

        StartConstantVoice(mixer, 0, music, MusicValue);
        Assert.True(mixer.TryAddDuckingEffect(music, NewDucking(attackSeconds: 0.0005f), dialogue));
        Render(mixer, 480);
        StartConstantVoice(mixer, 1, dialogueChild, DialogueValue);

        // The attack is 24 samples long: with the source mixed first, the very first block after the start is ducked to the end.
        // A source mixed after its target would be read one block late, and the music would still be at full level here.
        Assert.Equal((MusicValue * Gain(12f)) + DialogueValue, Last(Render(mixer, 480)), 2e-3f);
    }

    [Fact]
    public void ASourceBelowTheThreshold_OrSilencedByItsGain_DucksNothing()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var music = CreateBus(mixer);
        var dialogue = CreateBus(mixer);
        StartConstantVoice(mixer, 0, music, MusicValue);
        Assert.True(mixer.TryAddDuckingEffect(music, NewDucking(thresholdDb: -20f), dialogue));

        // 0.05 is -26 dBFS: under a -20 dB threshold.
        StartConstantVoice(mixer, 1, dialogue, 0.05f);

        for (var i = 0; i < 30; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal(MusicValue + 0.05f, Last(Render(mixer, 480)), 1e-3f);

        // 0.5 is above it, but the bus gain of the source brings it to -30 dBFS.
        Assert.True(mixer.TryStop(1, 1));
        StartConstantVoice(mixer, 2, dialogue, 0.5f);
        mixer.SetBusGain(dialogue, 0.03f);

        for (var i = 0; i < 30; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal(MusicValue + (0.5f * 0.03f), Last(Render(mixer, 480)), 1e-3f);

        // Gain back up: it ducks.
        mixer.SetBusGain(dialogue, 1f);

        for (var i = 0; i < 30; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal((MusicValue * Gain(12f)) + 0.5f, Last(Render(mixer, 480)), 2e-3f);
    }

    [Fact]
    public void ADuckingCycle_IsRefused_ButARelationInTheSameDirectionAsTheGraphIsNot()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var a = CreateBus(mixer);
        var b = CreateBus(mixer);
        var childOfA = CreateBus(mixer, a);

        // A bus cannot duck itself.
        Assert.False(mixer.IsDuckingAccepted(a, a));
        Assert.False(mixer.TryAddDuckingEffect(a, NewDucking(), a));

        // A is ducked by B: B -> A. The opposite relation is a cycle.
        Assert.True(mixer.TryAddDuckingEffect(a, NewDucking(), b));
        Assert.False(mixer.IsDuckingAccepted(b, a));
        Assert.False(mixer.TryAddDuckingEffect(b, NewDucking(), a));

        // Ducking a child by its parent would loop: the child feeds the parent, the parent would drive the child.
        Assert.False(mixer.IsDuckingAccepted(childOfA, a));

        // The other way is the same direction as the graph: the parent ducked by its child.
        Assert.True(mixer.IsDuckingAccepted(a, childOfA));

        // Through a send and through a chain of ducking relations.
        Assert.False(mixer.TrySetSend(a, b, 0.5f));
        Assert.True(mixer.TrySetSend(b, a, 0.5f));
        Assert.True(mixer.TryAddDuckingEffect(b, NewDucking(), childOfA));
        Assert.False(mixer.IsDuckingAccepted(childOfA, b));

        // A bus that does not exist.
        Assert.False(mixer.IsDuckingAccepted(a, 99));
        Assert.False(mixer.IsDuckingAccepted(-1, a));

        // A plain insert refuses a ducking: it needs its source.
        Assert.False(mixer.TryAddEffect(a, NewDucking()));
    }

    [Fact]
    public void RemovingTheDucking_RestoresTheTarget_AndFreesTheRelation()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var music = CreateBus(mixer);
        var dialogue = CreateBus(mixer);
        StartConstantVoice(mixer, 0, music, MusicValue);
        StartConstantVoice(mixer, 1, dialogue, DialogueValue);
        var ducking = NewDucking();
        Assert.True(mixer.TryAddDuckingEffect(music, ducking, dialogue));

        for (var i = 0; i < 50; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal((MusicValue * Gain(12f)) + DialogueValue, Last(Render(mixer, 480)), 2e-3f);

        Assert.True(mixer.TryRemoveEffect(music, ducking));
        Assert.Equal(MusicValue + DialogueValue, Last(Render(mixer, 480)), 1e-4f);

        // The reverse relation is no longer a cycle.
        Assert.True(mixer.IsDuckingAccepted(dialogue, music));
    }

    [Fact]
    public void TheDuckingSlotsAreSharedWithTheOtherInsertEffects_OnTheAudioSide()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var music = CreateBus(mixer);
        var dialogue = CreateBus(mixer);
        var ducking = NewDucking();
        Assert.True(mixer.TryAddEffect(music, new BiquadFilterEffect(BiquadFilterType.LowPass, 5000f)));
        Assert.True(mixer.TryAddDuckingEffect(music, ducking, dialogue));
        StartConstantVoice(mixer, 0, music, MusicValue);
        StartConstantVoice(mixer, 1, dialogue, DialogueValue);

        // The biquad is a low pass at its default frequency: a constant (DC) passes it, so the ducking after it still acts.
        for (var i = 0; i < 50; i++)
        {
            Render(mixer, 480);
        }

        Assert.Equal((MusicValue * Gain(12f)) + DialogueValue, Last(Render(mixer, 480)), 5e-3f);
    }

    [Fact]
    public void Render_WithDuckingRunning_DoesNotAllocate()
    {
        var mixer = new SoftwareMixer(OutputRate);

        for (var i = 1; i < SoftwareMixer.BusCapacity; i++)
        {
            CreateBus(mixer);
        }

        for (var voice = 0; voice < 64; voice++)
        {
            var samples = new short[48000];
            Array.Fill(samples, (short)3000);
            Assert.True(mixer.TryStartResidentStereoVoice(voice, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default.WithLooping(true), 0.3f, 0.3f, 1 + (voice % 31)));
        }

        // Eight targets, each ducked by a source created before and after it, so the order has to be rebuilt.
        for (var target = 2; target < 10; target++)
        {
            Assert.True(mixer.TryAddDuckingEffect(target, NewDucking(thresholdDb: -30f), 31 - target));
        }

        Assert.True(mixer.TryAddDuckingEffect(20, NewDucking(thresholdDb: -30f), 3));
        var buffer = new float[480 * 2];

        // Applies the commands, rebuilds the order and settles the envelopes.
        for (var i = 0; i < 5; i++)
        {
            mixer.Render(buffer, 480);
        }

        var before = AllocationWindow.Start();

        for (var round = 0; round < 50; round++)
        {
            mixer.Render(buffer, 480);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(buffer.All(float.IsFinite));
    }
}
