using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Voice stealing by priority on the software backend (plan T8.4): the victim is silenced for good, its stale handle
/// cannot reach the new voice that reuses its slot, a backend ramp of the victim does not leak to the new voice, and
/// the backend stereo voices and the music tracks are never victims.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceVoicePriorityOnSoftwareBackendTests
{
    private const int Block = 480;
    private const float Frame = 0.01f;
    private const float VictimLevel = 0.05f;
    private const float SurvivorLevel = 0.10f;
    private const float NewLevel = 0.20f;

    private sealed class Rig : IDisposable
    {
        public Rig(int capacity)
        {
            Output = new OfflineAudioOutput();
            Service = new AudioService(new SoftwareAudioBackend(Output, capacity));
            Service.MasterLimiter.IsEnabled = false; // the tests read exact levels
            Provider = new FakeAudioClipProvider();
            Service.ClipProvider = Provider;
        }

        public OfflineAudioOutput Output { get; }

        public AudioService Service { get; }

        public FakeAudioClipProvider Provider { get; }

        public AudioVoiceHandle Play(float level, int priority, string name)
        {
            var samples = new short[2 * 400000];
            Array.Fill(samples, (short)(level * 32768f));
            var asset = new SoundAsset
            {
                Name = name,
                AudioFileAssetId = Provider.Register(new PcmAudioClip(samples, 48000, 2)),
                Priority = priority,
            };

            return Service.PlaySound(asset);
        }

        public void Tick(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                Service.Update(Frame);
                Output.Pump(Block);
            }
        }

        public float[] LastBlock() => Output.LastBlock.ToArray();

        public void Dispose() => Service.Dispose();
    }

    private static void AssertSameBlock(float[] expected, float[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 1e-4f);
        }
    }

    private static float MaxDifference(float[] a, float[] b)
    {
        var max = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            max = Math.Max(max, Math.Abs(a[i] - b[i]));
        }

        return max;
    }

    private static float[] ReferenceWithoutTheVictim()
    {
        using var reference = new Rig(2);
        reference.Play(SurvivorLevel, 2, "survivor");
        reference.Play(NewLevel, 3, "new");
        reference.Tick(4);
        return reference.LastBlock();
    }

    [Fact]
    public void AfterASteal_TheOutputIsTheSurvivorAndTheNewVoiceOnly()
    {
        using var rig = new Rig(2);
        var victim = rig.Play(VictimLevel, 1, "victim");
        rig.Play(SurvivorLevel, 2, "survivor");
        rig.Tick(2);

        var stealer = rig.Play(NewLevel, 3, "new");
        Assert.True(stealer.IsValid);
        Assert.False(rig.Service.IsAlive(victim));
        Assert.Equal(1, rig.Service.StolenVoiceCount);
        rig.Tick(4);

        var rendered = rig.LastBlock();
        AssertSameBlock(ReferenceWithoutTheVictim(), rendered);

        // Control: with the victim still sounding the block would be louder.
        using var withVictim = new Rig(3);
        withVictim.Play(VictimLevel, 1, "victim");
        withVictim.Play(SurvivorLevel, 2, "survivor");
        withVictim.Play(NewLevel, 3, "new");
        withVictim.Tick(4);
        Assert.True(MaxDifference(withVictim.LastBlock(), rendered) > 0.01f);
    }

    [Fact]
    public void TheStaleHandleOfTheVictim_CannotReachTheNewVoiceOfTheSameSlot()
    {
        using var rig = new Rig(2);
        var victim = rig.Play(VictimLevel, 1, "victim");
        rig.Play(SurvivorLevel, 2, "survivor");
        rig.Tick(2);

        var stealer = rig.Play(NewLevel, 3, "new");
        Assert.Equal(victim.Index, stealer.Index);
        Assert.NotEqual(victim.Generation, stealer.Generation);

        // Same frame, before the pump.
        rig.Service.FadeVoice(victim, 0f, 0.1f);
        rig.Service.SetVoiceVolume(victim, 0f);
        rig.Service.Stop(victim);
        rig.Tick(4);

        Assert.True(rig.Service.IsAlive(stealer));
        Assert.False(rig.Service.IsFading(stealer));
        AssertSameBlock(ReferenceWithoutTheVictim(), rig.LastBlock());
    }

    [Fact]
    public void ABackendRampOfTheVictim_IsNotPassedToTheNewVoice()
    {
        using var rig = new Rig(2);
        var victim = rig.Play(VictimLevel, 1, "victim");
        rig.Play(SurvivorLevel, 2, "survivor");
        rig.Tick(2);
        rig.Service.FadeVoice(victim, 0f, 1f);
        rig.Tick(10);
        Assert.True(rig.Service.IsFading(victim));

        var stealer = rig.Play(NewLevel, 3, "new");
        Assert.True(stealer.IsValid);
        Assert.False(rig.Service.IsFading(stealer));
        Assert.Equal(1f, rig.Service.GetVoiceVolume(stealer), 5);
        rig.Tick(4);

        AssertSameBlock(ReferenceWithoutTheVictim(), rig.LastBlock());
    }

    [Fact]
    public void ABackendStereoVoiceAndAMusicTrack_AreNeverVictims()
    {
        using var rig = new Rig(2);
        var mono = new PcmAudioClip(new short[48000], 48000, 1);
        var stereo = rig.Service.PlayClipStereo(mono, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: 22050, formatChunkSize: 18);
        var track = rig.Service.Music.Play(new SoundAsset
        {
            Name = "theme",
            AudioFileAssetId = rig.Provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
        });
        Assert.True(stereo.IsValid);
        Assert.True(track.IsValid);

        var refused = rig.Play(NewLevel, SoundAsset.MaxPriority, "new");

        Assert.False(refused.IsValid);
        Assert.Equal(0, rig.Service.StolenVoiceCount);
        Assert.Equal(1, rig.Service.RefusedVoiceCount);
        Assert.True(rig.Service.IsAlive(stereo));
        Assert.True(rig.Service.Music.IsPlaying(track));
    }

    [Fact]
    public void WithAPriorityVoiceBesideThem_OnlyThatVoiceIsStolen()
    {
        using var rig = new Rig(3);
        var mono = new PcmAudioClip(new short[48000], 48000, 1);
        var stereo = rig.Service.PlayClipStereo(mono, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 1f);
        var wav = WavBuilder.CreatePcm16(sampleRate: 22050, channelCount: 2, sampleCount: 22050, formatChunkSize: 18);
        var track = rig.Service.Music.Play(new SoundAsset
        {
            Name = "theme",
            AudioFileAssetId = rig.Provider.RegisterStream(wav),
            BusName = AudioBusNames.Music,
            IsStreaming = true,
        });
        var victim = rig.Play(VictimLevel, 1, "victim");
        Assert.True(stereo.IsValid && track.IsValid && victim.IsValid);

        var stealer = rig.Play(NewLevel, 2, "new");

        Assert.True(stealer.IsValid);
        Assert.False(rig.Service.IsAlive(victim));
        Assert.True(rig.Service.IsAlive(stereo));
        Assert.True(rig.Service.Music.IsPlaying(track));
        Assert.Equal(1, rig.Service.StolenVoiceCount);
    }
}
