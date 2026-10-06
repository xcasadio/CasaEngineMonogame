using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Application of a mixer asset to the live mixer (plan decision P46). Log-capturing tests need the serialized collection.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioMixerAssetApplierTests
{
    private const int Block = 480;

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Warnings.Add(msg);
        public void WriteError(string msg) { }
    }

    private static AudioMixerBusData Bus(string name, string parent = "Master", float volume = 1f)
        => new() { Name = name, Parent = parent, Volume = volume };

    private static AudioMixerAsset Asset(params AudioMixerBusData[] buses)
    {
        var asset = new AudioMixerAsset { Name = "Project mixer" };
        asset.Buses.AddRange(buses);
        return asset;
    }

    private static AudioService NewService() => new(new FakeAudioBackend());

    private static List<string> Capture(Action action)
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);
        try
        {
            action();
            return logger.Warnings;
        }
        finally
        {
            Logs.Close();
        }
    }

    [Fact]
    public void Apply_CreatesTheBusesUnderTheirParents_AndSetsTheVolumes()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        Assert.False(applier.IsApplied);

        applier.Apply(Asset(Bus("Ambience", "Music", 0.3f), Bus("Reverb", "Master", 0.5f), Bus("Sfx", "Master", 0.8f)));

        Assert.True(applier.IsApplied);
        Assert.Empty(applier.LastProblems);
        Assert.Equal(0.3f, service.Mixer.GetBus("Ambience").Volume);
        Assert.Equal("Music", service.Mixer.GetBus("Ambience").Parent.Name);
        Assert.Equal(0.5f, service.Mixer.GetBus("Reverb").Volume);
        Assert.Equal(0.8f, service.Mixer.GetBus("Sfx").Volume);
        Assert.Equal(1f, service.Mixer.GetBus("Music").Volume);
        Assert.Equal(int.MaxValue, applier.BusCapacity);
    }

    [Fact]
    public void Apply_ASecondIdenticalApplication_ChangesNothing()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A", "Master", 0.5f);
        a.Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.HighPass, 300f));
        a.Effects.Add(new AudioMixerDuckingEffectData("B"));
        a.Sends.Add(new AudioMixerSendData("Music", 0.4f));
        var asset = Asset(a, Bus("B"));

        Capture(() => applier.Apply(asset));
        var effects = service.Mixer.GetBus("A").Effects.ToArray();
        int version = service.Mixer.Version, effectsVersion = service.Mixer.EffectsVersion, sendsVersion = service.Mixer.SendsVersion;
        var busCount = service.Mixer.Buses.Count;

        var warnings = Capture(() => applier.Apply(asset));

        Assert.Empty(warnings);
        Assert.Equal(version, service.Mixer.Version);
        Assert.Equal(effectsVersion, service.Mixer.EffectsVersion);
        Assert.Equal(sendsVersion, service.Mixer.SendsVersion);
        Assert.Equal(busCount, service.Mixer.Buses.Count);
        Assert.Equal(effects.Length, service.Mixer.GetBus("A").Effects.Count);
        for (var i = 0; i < effects.Length; i++)
        {
            Assert.Same(effects[i], service.Mixer.GetBus("A").Effects[i]);
        }
    }

    [Fact]
    public void Apply_BuildsTheFourEffectTypes_WithTheirValues()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.HighPass, 300f, 1.2f, 3f));
        a.Effects.Add(new AudioMixerCompressorEffectData(-24f, 6f, 3f, 0.02f, 0.2f, 2f));
        a.Effects.Add(new AudioMixerReverbEffectData(0.7f, 0.4f, 0.5f, 0.6f, 0.8f));
        a.Effects.Add(new AudioMixerDuckingEffectData("Voice", 9f, -30f, 0.05f, 0.5f));

        Capture(() => applier.Apply(Asset(a)));

        var effects = service.Mixer.GetBus("A").Effects;
        Assert.Equal(4, effects.Count);
        var biquad = Assert.IsType<BiquadFilterEffect>(effects[0]);
        Assert.Equal(BiquadFilterType.HighPass, biquad.Type);
        Assert.Equal(300f, biquad.FrequencyHz);
        Assert.Equal(1.2f, biquad.Q);
        Assert.Equal(3f, biquad.GainDb);
        var compressor = Assert.IsType<CompressorEffect>(effects[1]);
        Assert.Equal(-24f, compressor.ThresholdDb);
        Assert.Equal(6f, compressor.Ratio);
        Assert.Equal(3f, compressor.KneeDb);
        Assert.Equal(0.02f, compressor.AttackSeconds);
        Assert.Equal(0.2f, compressor.ReleaseSeconds);
        Assert.Equal(2f, compressor.MakeupGainDb);
        var reverb = Assert.IsType<ReverbEffect>(effects[2]);
        Assert.Equal(0.7f, reverb.RoomSize);
        Assert.Equal(0.4f, reverb.Damping);
        Assert.Equal(0.5f, reverb.Wet);
        Assert.Equal(0.6f, reverb.Dry);
        Assert.Equal(0.8f, reverb.StereoSeparation);
        var ducking = Assert.IsType<DuckingEffect>(effects[3]);
        Assert.Same(service.Mixer.GetBus("Voice"), ducking.Source);
        Assert.Equal(9f, ducking.DepthDb);
        Assert.Equal(-30f, ducking.ThresholdDb);
        Assert.Equal(0.05f, ducking.AttackSeconds);
        Assert.Equal(0.5f, ducking.ReleaseSeconds);
    }

    [Fact]
    public void Apply_SetsTheSends_IncludingToABusDeclaredAfterItsSource()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Sends.Add(new AudioMixerSendData("Return", 0.4f));
        a.Sends.Add(new AudioMixerSendData("Music", 0.2f));

        applier.Apply(Asset(a, Bus("Return")));

        var bus = service.Mixer.GetBus("A");
        Assert.Equal(0.4f, bus.GetSend(service.Mixer.GetBus("Return")));
        Assert.Equal(0.2f, bus.GetSend(service.Mixer.GetBus("Music")));
    }

    [Fact]
    public void Apply_ADuckingWhoseSourceIsDeclaredAfterItsTarget_IsApplied()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var target = Bus("Bed");
        target.Effects.Add(new AudioMixerDuckingEffectData("Dialogue"));

        applier.Apply(Asset(target, Bus("Dialogue")));

        var ducking = Assert.IsType<DuckingEffect>(Assert.Single(service.Mixer.GetBus("Bed").Effects));
        Assert.Equal("Dialogue", ducking.Source.Name);
        Assert.Empty(applier.LastProblems);
    }

    [Fact]
    public void Apply_AChangedParameter_EditsTheSameInstance()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Effects.Add(new AudioMixerCompressorEffectData(-20f));
        Capture(() => applier.Apply(Asset(a)));
        var instance = service.Mixer.GetBus("A").Effects[0];
        var effectsVersion = service.Mixer.EffectsVersion;

        var changed = Bus("A");
        changed.Effects.Add(new AudioMixerCompressorEffectData(-30f, 8f));
        Capture(() => applier.Apply(Asset(changed)));

        Assert.Same(instance, Assert.Single(service.Mixer.GetBus("A").Effects));
        Assert.Equal(-30f, ((CompressorEffect)instance).ThresholdDb);
        Assert.Equal(8f, ((CompressorEffect)instance).Ratio);
        Assert.Equal(effectsVersion, service.Mixer.EffectsVersion);
    }

    [Fact]
    public void Apply_AChangedType_ReplacesTheInstance_AndTheFollowingOnes()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Effects.Add(new AudioMixerBiquadEffectData());
        a.Effects.Add(new AudioMixerReverbEffectData());
        Capture(() => applier.Apply(Asset(a)));
        var first = service.Mixer.GetBus("A").Effects[0];
        var second = service.Mixer.GetBus("A").Effects[1];

        var changed = Bus("A");
        changed.Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.HighPass));
        changed.Effects.Add(new AudioMixerCompressorEffectData());
        Capture(() => applier.Apply(Asset(changed)));

        var effects = service.Mixer.GetBus("A").Effects;
        Assert.Equal(2, effects.Count);
        Assert.Same(first, effects[0]);
        Assert.Equal(BiquadFilterType.HighPass, ((BiquadFilterEffect)first).Type);
        Assert.IsType<CompressorEffect>(effects[1]);
        Assert.Null(second.Bus);
    }

    [Fact]
    public void Apply_AChangedDuckingSource_ReplacesTheInstance()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Effects.Add(new AudioMixerDuckingEffectData("Voice", 6f));
        Capture(() => applier.Apply(Asset(a)));
        var old = (DuckingEffect)service.Mixer.GetBus("A").Effects[0];

        var changed = Bus("A");
        changed.Effects.Add(new AudioMixerDuckingEffectData("Ui", 6f));
        Capture(() => applier.Apply(Asset(changed)));

        var live = service.Mixer.GetBus("A").Effects;
        var replacement = Assert.IsType<DuckingEffect>(Assert.Single(live));
        Assert.Same(service.Mixer.GetBus("Ui"), replacement.Source);
        Assert.NotSame(old, replacement);
        Assert.DoesNotContain(old, live);
        Assert.Null(old.Bus);
    }

    [Fact]
    public void Apply_RemovedEffectsAndSends_AreTakenOff()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Effects.Add(new AudioMixerBiquadEffectData());
        a.Effects.Add(new AudioMixerReverbEffectData());
        a.Sends.Add(new AudioMixerSendData("Music", 0.5f));
        a.Sends.Add(new AudioMixerSendData("Sfx", 0.5f));
        Capture(() => applier.Apply(Asset(a)));

        var smaller = Bus("A");
        smaller.Effects.Add(new AudioMixerBiquadEffectData());
        smaller.Sends.Add(new AudioMixerSendData("Sfx", 0.25f));
        Capture(() => applier.Apply(Asset(smaller)));

        var bus = service.Mixer.GetBus("A");
        Assert.IsType<BiquadFilterEffect>(Assert.Single(bus.Effects));
        Assert.Equal(0f, bus.GetSend(service.Mixer.GetBus("Music")));
        Assert.Equal(0.25f, bus.GetSend(service.Mixer.GetBus("Sfx")));
        Assert.Single(bus.Sends);
    }

    [Fact]
    public void Release_TakesOffWhatWasAdded_RestoresTheVolumes_AndKeepsTheBuses()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var music = Bus("Music", "Master", 0.4f);
        music.Effects.Add(new AudioMixerBiquadEffectData());
        music.Sends.Add(new AudioMixerSendData("Extra", 0.5f));
        var extra = Bus("Extra", "Master", 0.3f);
        Capture(() => applier.Apply(Asset(music, extra)));
        Assert.Equal(0.4f, service.Mixer.GetBus("Music").Volume);

        applier.Release();

        var liveMusic = service.Mixer.GetBus("Music");
        Assert.False(applier.IsApplied);
        Assert.Empty(applier.LastProblems);
        Assert.Equal(1f, liveMusic.Volume);
        Assert.Empty(liveMusic.Effects);
        Assert.Empty(liveMusic.Sends);
        Assert.True(service.Mixer.TryGetBus("Extra", out var liveExtra));
        Assert.Equal(1f, liveExtra.Volume);
        Assert.False(applier.TryApplyBusVolume("Music", 0.2f));
    }

    [Fact]
    public void ProjectA_ThenB_ThenNone_GivesBackWhatTheFormerOneChanged()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("Sfx", "Master", 0.5f);
        a.Effects.Add(new AudioMixerReverbEffectData());
        a.Sends.Add(new AudioMixerSendData("Music", 0.5f));
        Capture(() => applier.Apply(Asset(a, Bus("OnlyA"))));

        var b = Bus("Music", "Master", 0.7f);
        b.Effects.Add(new AudioMixerBiquadEffectData());
        Capture(() => applier.Apply(Asset(b, Bus("OnlyB"))));

        var sfx = service.Mixer.GetBus("Sfx");
        Assert.Equal(1f, sfx.Volume);
        Assert.Empty(sfx.Effects);
        Assert.Empty(sfx.Sends);
        Assert.Equal(0.7f, service.Mixer.GetBus("Music").Volume);
        Assert.IsType<BiquadFilterEffect>(Assert.Single(service.Mixer.GetBus("Music").Effects));
        Assert.True(service.Mixer.TryGetBus("OnlyA", out _));
        Assert.True(service.Mixer.TryGetBus("OnlyB", out _));

        applier.Release();

        Assert.Equal(1f, service.Mixer.GetBus("Music").Volume);
        Assert.Empty(service.Mixer.GetBus("Music").Effects);
    }

    [Fact]
    public void Apply_NeverTouchesMasterEditorMutesOrTheGamesOwnEffects()
    {
        using var service = NewService();
        var master = service.Mixer.GetBus(AudioBusNames.Master);
        var editor = service.Mixer.GetBus(AudioBusNames.Editor);
        master.Volume = 0.3f;
        master.IsMuted = true;
        editor.Volume = 0.6f;
        service.Mixer.GetBus(AudioBusNames.Voice).IsMuted = true;
        var gameEffect = new CompressorEffect();
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        sfx.AddEffect(gameEffect);
        var gameSend = service.Mixer.GetBus(AudioBusNames.Music);
        sfx.SetSend(gameSend, 0.9f);
        var limiter = service.MasterLimiter.IsEnabled;

        var mixer = Asset(Bus("Master", "Master", 0f), Bus("Editor", "Master", 0f), Bus("Voice", "Master", 0.5f), Bus("Sfx"));
        mixer.Buses[3].Effects.Add(new AudioMixerBiquadEffectData());
        mixer.Buses[3].Sends.Add(new AudioMixerSendData("Ui", 0.5f));
        var applier = new AudioMixerAssetApplier(service);
        Capture(() => applier.Apply(mixer));
        Capture(() => applier.Apply(mixer));

        Assert.Equal(0.3f, master.Volume);
        Assert.True(master.IsMuted);
        Assert.Equal(0.6f, editor.Volume);
        Assert.True(service.Mixer.GetBus(AudioBusNames.Voice).IsMuted);
        Assert.Equal(0.5f, service.Mixer.GetBus(AudioBusNames.Voice).Volume);
        Assert.Equal(limiter, service.MasterLimiter.IsEnabled);
        Assert.Same(gameEffect, sfx.Effects[0]);
        Assert.Equal(2, sfx.Effects.Count);
        Assert.Equal(0.9f, sfx.GetSend(gameSend));

        applier.Release();

        Assert.Same(gameEffect, Assert.Single(sfx.Effects));
        Assert.Equal(0.9f, sfx.GetSend(gameSend));
        Assert.Equal(1f, service.Mixer.GetBus(AudioBusNames.Voice).Volume);
        Assert.True(service.Mixer.GetBus(AudioBusNames.Voice).IsMuted);
        Assert.Equal(0.3f, master.Volume);
        Assert.True(master.IsMuted);
    }

    [Fact]
    public void Apply_ABusFullOfForeignEffects_WarnsWithoutThrowing()
    {
        using var service = NewService();
        var sfx = service.Mixer.GetBus(AudioBusNames.Sfx);
        for (var i = 0; i < AudioBus.MaxEffects; i++)
        {
            sfx.AddEffect(new CompressorEffect());
        }

        var applier = new AudioMixerAssetApplier(service);
        var asset = Bus("Sfx");
        asset.Effects.Add(new AudioMixerReverbEffectData());

        var warnings = Capture(() => applier.Apply(Asset(asset, Bus("After", "Master", 0.5f))));

        Assert.Equal(AudioBus.MaxEffects, sfx.Effects.Count);
        Assert.All(sfx.Effects, e => Assert.IsType<CompressorEffect>(e));
        Assert.Contains(warnings, w => w.Contains("could not be added") && w.Contains("Sfx"));
        Assert.Equal(0.5f, service.Mixer.GetBus("After").Volume);
    }

    [Fact]
    public void Apply_AsetWithEffects_UnderABackendWithoutBuses_WarnsOnce()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var a = Bus("A");
        a.Effects.Add(new AudioMixerBiquadEffectData());
        var asset = Asset(a);

        var warnings = Capture(() =>
        {
            applier.Apply(asset);
            applier.Apply(asset);
        });

        Assert.Single(warnings, w => w.Contains("no bus support"));
    }

    [Fact]
    public void Apply_LogsTheProblems_UnlessAskedNot()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);
        var asset = Asset(Bus("Lost", "Nowhere"));

        var logged = Capture(() => applier.Apply(asset));
        var silent = Capture(() => applier.Apply(asset, false));

        Assert.Single(logged);
        Assert.Empty(silent);
        Assert.Single(applier.LastProblems);
    }

    [Fact]
    public void TryApplyBusVolume_SetsTheVolumeOfABusOfTheAsset_AndRestoresItOnRelease()
    {
        using var service = NewService();
        var applier = new AudioMixerAssetApplier(service);

        Assert.False(applier.TryApplyBusVolume("Music", 0.5f));

        applier.Apply(Asset(Bus("Music", "Master", 0.8f), Bus("Fx")));

        Assert.True(applier.TryApplyBusVolume("Music", 0.25f));
        Assert.True(applier.TryApplyBusVolume("fx", 0.1f));
        Assert.Equal(0.25f, service.Mixer.GetBus("Music").Volume);
        Assert.Equal(0.1f, service.Mixer.GetBus("Fx").Volume);
        Assert.False(applier.TryApplyBusVolume("Sfx", 0.5f)); // live but not in the asset
        Assert.False(applier.TryApplyBusVolume("Nope", 0.5f));
        Assert.Equal(1f, service.Mixer.GetBus("Sfx").Volume);

        applier.Release();

        Assert.Equal(1f, service.Mixer.GetBus("Music").Volume);
    }

    [Fact]
    public void Apply_UnderTheSoftwareBackend_ABusUnderSfxAtHalfRendersHalfOfTheVoice()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        service.MasterLimiter.IsEnabled = false;
        var applier = new AudioMixerAssetApplier(service);
        Assert.Equal(32, applier.BusCapacity);

        applier.Apply(Asset(Bus("Layer", AudioBusNames.Sfx, 0.5f)));

        var samples = new short[2 * 200000];
        Array.Fill(samples, (short)16384);
        service.PlayClip(new PcmAudioClip(samples, 48000, 2), "Layer", AudioVoiceParameters.Default);
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);

        Assert.Equal(0.25f, output.Pump(Block), 3);

        Assert.True(applier.TryApplyBusVolume("Layer", 1f));
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        Assert.Equal(0.5f, output.Pump(Block), 3);
    }

    [Fact]
    public void Apply_UnderTheSoftwareBackend_TheBusBeyondTheCapacityIsRefusedWithAProblem()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        var applier = new AudioMixerAssetApplier(service);
        var asset = new AudioMixerAsset { Name = "Crowded" };
        for (var i = 0; i < 27; i++) // six live buses plus 27 is 33
        {
            asset.Buses.Add(Bus("Bus" + i));
        }

        var warnings = Capture(() => applier.Apply(asset));

        Assert.Equal(32, service.Mixer.Buses.Count);
        Assert.False(service.Mixer.TryGetBus("Bus26", out _));
        var problem = Assert.Single(applier.LastProblems);
        Assert.Contains("32", problem);
        Assert.Contains("Bus26", problem);
        Assert.Single(warnings);
    }
}
