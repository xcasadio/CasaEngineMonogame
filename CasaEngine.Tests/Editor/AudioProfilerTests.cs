using System.Globalization;
using CasaEngine.Core.Logging;
using CasaEngine.Editor.Controls;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Tests.Audio;
using CasaEngine.Tests.ContentBrowser;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The "Audio" panel of the editor (plan T6.2, decision P23): the dBFS conversion, the peak hold and fall, the two rates (the
/// meters read on every update, the texts at most four times per second and only when something changed), the metering read
/// without allocation, and the state under a backend without the metering capability. The logic runs without a GPU, on the
/// real <see cref="SoftwareAudioBackend"/> driven block by block by an <see cref="OfflineAudioOutput"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioProfilerTests
{
    private const int Block = 480;
    private const float Frame = 0.016f;

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

    /// <summary>A software backend on an offline output, its service and a profiler model watching it.</summary>
    private sealed class Rig : IDisposable
    {
        public Rig(AudioMixer mixer = null)
        {
            Output = new OfflineAudioOutput();
            Service = new AudioService(new SoftwareAudioBackend(Output, 16), mixer);
            Service.MasterLimiter.IsEnabled = false;
            Model = new AudioProfilerModel(() => Service);
        }

        public OfflineAudioOutput Output { get; }

        public AudioService Service { get; }

        public AudioProfilerModel Model { get; }

        public AudioMeterTrack Bus(string name) => Model.Buses.Single(track => track.Name == name);

        public void PlayConstant(string bus, short value, int frames)
        {
            Service.PlayClip(ConstantStereoClip(value, frames), bus, AudioVoiceParameters.Default);
            Service.Update(0.01f);
        }

        public void Pump(int blocks)
        {
            for (var i = 0; i < blocks; i++)
            {
                Output.Pump(Block);
            }
        }

        public void Dispose() => Service.Dispose();
    }

    private static PcmAudioClip ConstantStereoClip(short value, int frames)
    {
        var samples = new short[2 * frames];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, 48000, 2);
    }

    // ───────────────────────── dBFS conversion ─────────────────────────

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0.5f, -6.0206f)]
    [InlineData(0.1f, -20f)]
    [InlineData(2f, 6.0206f)]
    public void ToDb_ConvertsAnAmplitudeToDbfs(float amplitude, float expected)
    {
        Assert.Equal(expected, AudioMeterScale.ToDb(amplitude), 1e-3f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    [InlineData(1e-6f)]
    public void ToDb_SilenceAndInvalidAmplitudes_GiveTheFloor(float amplitude)
    {
        Assert.Equal(AudioMeterScale.FloorDb, AudioMeterScale.ToDb(amplitude));
    }

    [Fact]
    public void ToDb_AnInfiniteAmplitude_IsClampedToTheCeiling()
    {
        Assert.Equal(AudioMeterScale.CeilingDb, AudioMeterScale.ToDb(float.PositiveInfinity));
    }

    [Theory]
    [InlineData(AudioMeterScale.FloorDb, "-inf")]
    [InlineData(-6.0206f, "-6.0")]
    [InlineData(0f, "0.0")]
    [InlineData(3.04f, "+3.0")]
    [InlineData(-0.04f, "0.0")]
    public void ADisplayedLevel_IsRoundedToATenthOfADecibel(float db, string expected)
    {
        Assert.Equal(expected, AudioMeterTrack.FormatTenths(AudioMeterTrack.ToTenths(db)));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(-30f, 0.5f)]
    [InlineData(-60f, 0f)]
    [InlineData(-90f, 0f)]
    [InlineData(6f, 1f)]
    public void TheMeterScale_RunsFromMinus60DbfsToFullScale(float db, float expected)
    {
        Assert.Equal(expected, AudioMeterControl.ToFraction(db), 1e-5f);
    }

    // ───────────────────────── peak hold and fall ─────────────────────────

    private static AudioLevel Level(float peak, float rms = 0f, int overs = 0) => new(peak, peak, rms, overs);

    [Fact]
    public void ThePeak_RisesAtOnce_AndFallsAtTheNamedRate()
    {
        var track = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);

        track.Integrate(Level(0.5f, 0.25f), 0.016f);

        Assert.Equal(-6.0206f, track.PeakDb, 1e-3f);
        Assert.Equal(-12.0412f, track.RmsDb, 1e-3f);

        // 0.5 s of silence: the bars fall by FallDbPerSecond * 0.5.
        for (var i = 0; i < 5; i++)
        {
            track.Integrate(default, 0.1f);
        }

        Assert.Equal(-6.0206f - (AudioMeterScale.FallDbPerSecond * 0.5f), track.PeakDb, 1e-3f);
        Assert.Equal(-12.0412f - (AudioMeterScale.FallDbPerSecond * 0.5f), track.RmsDb, 1e-3f);
    }

    [Fact]
    public void ThePeakHold_StaysForTheHoldTime_ThenFalls()
    {
        var track = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);
        track.Integrate(Level(0.5f), 0.016f);
        var held = track.HoldDb;

        // Exactly the hold time: still held.
        track.Integrate(default, AudioMeterScale.PeakHoldSeconds);
        Assert.Equal(held, track.HoldDb);

        // Past it, it falls like the bars (here from the held value, since the bar is already far below).
        track.Integrate(default, 0.1f);
        Assert.Equal(held - (AudioMeterScale.FallDbPerSecond * 0.1f), track.HoldDb, 1e-3f);
        track.Integrate(default, 0.5f);
        Assert.Equal(held - (AudioMeterScale.FallDbPerSecond * 0.6f), track.HoldDb, 1e-3f);
    }

    [Fact]
    public void ALowerPeak_DoesNotRestartTheHold_AndAHigherOneDoes()
    {
        var track = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);
        track.Integrate(Level(0.5f), 0.016f);
        var held = track.HoldDb;

        track.Integrate(default, 1.0f);
        track.Integrate(Level(0.1f), 0.4f);
        Assert.Equal(held, track.HoldDb);

        // 0.1 s later the hold time (1.5 s) is over: the lower peak did not restart it.
        track.Integrate(Level(0.1f), 0.2f);
        Assert.True(track.HoldDb < held);

        track.Integrate(Level(0.9f), 0.016f);
        Assert.Equal(AudioMeterScale.ToDb(0.9f), track.HoldDb, 1e-3f);
        track.Integrate(default, AudioMeterScale.PeakHoldSeconds);
        Assert.Equal(AudioMeterScale.ToDb(0.9f), track.HoldDb, 1e-3f);
    }

    [Fact]
    public void TheHoldMarker_NeverFallsBelowTheLiveBar()
    {
        var track = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);
        track.Integrate(Level(0.5f), 0.016f);
        track.Integrate(default, AudioMeterScale.PeakHoldSeconds + 0.1f);

        // A steady lower signal: the bar settles at it, and so does the marker.
        for (var i = 0; i < 200; i++)
        {
            track.Integrate(Level(0.05f), 0.05f);
        }

        Assert.Equal(AudioMeterScale.ToDb(0.05f), track.PeakDb, 1e-3f);
        Assert.Equal(AudioMeterScale.ToDb(0.05f), track.HoldDb, 1e-3f);
    }

    [Fact]
    public void TheOvers_AreCountedAndLightTheLampForTheHoldTime()
    {
        var output = new AudioMeterTrack("Output", 0, -1, isOutput: true);

        Assert.False(output.IsOverLit);

        output.Integrate(Level(1f, 0.5f, overs: 3), 0.016f);
        output.Integrate(Level(1f, 0.5f, overs: 2), 0.016f);

        Assert.Equal(5, output.OversTotal);
        Assert.True(output.IsOverLit);

        output.Integrate(default, AudioMeterScale.PeakHoldSeconds + 0.1f);

        Assert.False(output.IsOverLit);
        Assert.Equal(5, output.OversTotal);
    }

    // ───────────────────────── text: only when a displayed value changes ─────────────────────────

    [Fact]
    public void TheTexts_AreRebuiltOnlyWhenADisplayedTenthOfADecibelChanges()
    {
        var track = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);

        Assert.True(track.RefreshText());
        Assert.Equal("-inf", track.PeakText);
        Assert.False(track.RefreshText());

        track.Integrate(Level(0.5f, 0.25f), 0.016f);
        Assert.True(track.RefreshText());
        Assert.Equal("-6.0", track.PeakText);
        Assert.Equal("-6.0", track.HoldText);
        Assert.Equal("-12.0", track.RmsText);
        Assert.False(track.RefreshText());

        // The same level again: nothing displayed changed (the peak and hold stay at -6.0, the RMS is unchanged).
        track.Integrate(Level(0.5f, 0.25f), 0.016f);
        Assert.False(track.RefreshText());

        // A change below a tenth of a dB is not a change of the text.
        track.Integrate(Level(0.4999f, 0.25f), 0.016f);
        Assert.False(track.RefreshText());
    }

    [Fact]
    public void ABusWithoutALevel_ShowsNotAvailable()
    {
        var track = new AudioMeterTrack("Extra", 1, -1, isOutput: false);

        Assert.False(track.HasLevel);
        Assert.True(track.RefreshText());
        Assert.Equal("n/a", track.PeakText);
        Assert.False(track.RefreshText());
    }

    [Fact]
    public void WithNothingToShow_TheTextsAreBuiltOnce()
    {
        using var rig = new Rig();

        for (var i = 0; i < 300; i++)
        {
            rig.Model.Update(Frame);
        }

        // The first update builds every text; five seconds of silence then change nothing.
        Assert.Equal(1, rig.Model.TextRebuildCount);
    }

    [Fact]
    public void WithASteadyLevel_TheTextsAreNotRebuiltAgain()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 200000);
        rig.Pump(4);
        rig.Model.Update(Frame);
        var rebuilds = rig.Model.TextRebuildCount;

        for (var i = 0; i < 200; i++)
        {
            rig.Pump(1);
            rig.Model.Update(Frame);
        }

        Assert.Equal(rebuilds, rig.Model.TextRebuildCount);
        Assert.Equal("-6.0", rig.Bus(AudioBusNames.Sfx).PeakText);
    }

    [Fact]
    public void TheTexts_AreRebuiltAtMostFourTimesPerSecond_EvenWhenTheLevelChangesOnEveryFrame()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 480 * 700);
        var sfx = rig.Service.Mixer.GetBus(AudioBusNames.Sfx);
        var rebuiltAt = new List<double>();
        double now = 0;

        for (var i = 0; i < 600; i++)
        {
            // A different bus gain on every frame: the displayed levels change on every frame.
            sfx.Volume = 0.1f + ((i % 7) * 0.12f);
            rig.Service.Update(Frame);
            rig.Pump(1);

            if (rig.Model.Update(Frame))
            {
                rebuiltAt.Add(now);
            }

            now += Frame;
        }

        Assert.True(rebuiltAt.Count > 20, $"the texts should follow the changing level (rebuilt {rebuiltAt.Count} times)");
        Assert.Equal(0, rebuiltAt[0]);

        for (var i = 1; i < rebuiltAt.Count; i++)
        {
            Assert.True(rebuiltAt[i] - rebuiltAt[i - 1] >= AudioProfilerModel.TextIntervalSeconds - 1e-4,
                $"rebuilds {i - 1} and {i} are {rebuiltAt[i] - rebuiltAt[i - 1]:F3} s apart");
        }

        // Any window of one second holds at most four rebuilds.
        for (var i = 0; i + 4 < rebuiltAt.Count; i++)
        {
            Assert.True(rebuiltAt[i + 4] - rebuiltAt[i] >= 1.0 - 1e-3);
        }
    }

    [Fact]
    public void AnIsolatedOneBlockPeak_BetweenTwoTextRebuilds_ShowsInThePeakHoldAndInTheNextText()
    {
        using var rig = new Rig();

        // Time 0: the first update builds the texts. Everything is silent.
        Assert.True(rig.Model.Update(Frame));
        Assert.Equal("-inf", rig.Bus(AudioBusNames.Sfx).HoldText);
        var rebuilds = rig.Model.TextRebuildCount;

        for (var i = 0; i < 5; i++)
        {
            Assert.False(rig.Model.Update(Frame));
        }

        // One block of 0.5 (-6.02 dBFS) on Sfx, then silence: published and gone before the next read.
        rig.PlayConstant(AudioBusNames.Sfx, 16384, Block);
        rig.Pump(4);

        Assert.False(rig.Model.Update(Frame));
        Assert.Equal(rebuilds, rig.Model.TextRebuildCount);
        Assert.Equal(-6.0206f, rig.Bus(AudioBusNames.Sfx).HoldDb, 1e-3f);
        Assert.Equal(-6.0206f, rig.Bus(AudioBusNames.Master).HoldDb, 1e-3f);
        Assert.Equal(-6.0206f, rig.Model.Output!.HoldDb, 1e-3f);

        // The next rebuild, no block published since, shows it.
        var rebuiltNow = false;
        for (var i = 0; i < 40 && !rebuiltNow; i++)
        {
            rebuiltNow = rig.Model.Update(Frame);
        }

        Assert.True(rebuiltNow);
        Assert.Equal("-6.0", rig.Bus(AudioBusNames.Sfx).HoldText);
        Assert.Equal("-6.0", rig.Bus(AudioBusNames.Master).HoldText);
        Assert.Equal("-6.0", rig.Model.Output.HoldText);

        // The bar fell in the meantime, the marker did not.
        Assert.NotEqual("-6.0", rig.Bus(AudioBusNames.Sfx).PeakText);
        Assert.Equal("-inf", rig.Bus(AudioBusNames.Music).HoldText);
    }

    [Fact]
    public void EveryBlockPublishedSinceTheLastRead_IsIntegrated()
    {
        using var rig = new Rig();
        rig.Model.Update(Frame);

        // A quiet voice, then a loud one-block voice in the middle of fifteen blocks.
        rig.PlayConstant(AudioBusNames.Sfx, 1638, 480 * 40);
        rig.Pump(5);
        rig.PlayConstant(AudioBusNames.Ui, 29491, Block);
        rig.Pump(10);

        rig.Model.Update(Frame);

        Assert.Equal(AudioMeterScale.ToDb(0.9f), rig.Bus(AudioBusNames.Ui).HoldDb, 0.01f);
        Assert.Equal(AudioMeterScale.ToDb(0.05f), rig.Bus(AudioBusNames.Sfx).HoldDb, 0.01f);
    }

    // ───────────────────────── the content of the panel ─────────────────────────

    [Fact]
    public void WithTheSoftwareBackend_OneMeterPerBusAndOneForTheOutput()
    {
        using var rig = new Rig();
        rig.Model.Update(Frame);

        var expected = rig.Service.Mixer.Buses.Select(bus => bus.Name).ToArray();

        Assert.True(rig.Model.IsMeteringAvailable);
        Assert.Equal(expected, rig.Model.Buses.Select(track => track.Name).ToArray());
        Assert.Equal(AudioBusNames.Master, rig.Model.Buses[0].Name);
        Assert.All(rig.Model.Buses.Skip(1), track => Assert.Equal(1, track.Depth));
        Assert.All(rig.Model.Buses, track => Assert.True(track.HasLevel));
        Assert.Equal(rig.Model.Buses.Length, rig.Model.Buses.Select(track => track.BackendIndex).Distinct().Count());
        Assert.NotNull(rig.Model.Output);
        Assert.True(rig.Model.Output!.IsOutput);
    }

    [Fact]
    public void TheStatistics_ComeFromTheServiceAndTheSoftwareBackend()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 200000);
        rig.Pump(2);
        rig.Model.Update(Frame);

        var lines = rig.Model.StatLines;

        Assert.Equal("Backend: SoftwareAudioBackend (output running)", lines[0]);
        Assert.Equal("Voices: 1 active of 16, 0 refused", lines[1]);
        Assert.Equal("Output: 48000 Hz, lead 40 ms, 0 underruns", lines[2]);
        Assert.Equal("Dropped: 0 stream chunks, 0 events", lines[3]);
        Assert.Equal("SPU: hosted by the backend", lines[4]);
        Assert.Equal("Meters: output overs 0, missed blocks 0", lines[5]);
    }

    [Fact]
    public void ABusAddedLater_GetsItsMeter_AndTheOthersKeepTheirState()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, Block);
        rig.Pump(2);
        rig.Model.Update(Frame);
        var version = rig.Model.StructureVersion;
        var sfx = rig.Bus(AudioBusNames.Sfx);
        var hold = sfx.HoldDb;
        Assert.True(hold > AudioMeterScale.FloorDb);

        rig.Service.Mixer.CreateBus("Ambience", AudioBusNames.Music);
        rig.Model.Update(Frame);

        Assert.NotEqual(version, rig.Model.StructureVersion);
        Assert.Same(sfx, rig.Bus(AudioBusNames.Sfx));
        Assert.Equal(hold, sfx.HoldDb, 0.5f);
        Assert.Equal(2, rig.Bus("Ambience").Depth);
        Assert.True(rig.Bus("Ambience").HasLevel);
    }

    [Fact]
    public void ABusBeyondTheBackendCapacity_HasNoLevel_AndIsShownNotAvailable()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        for (var i = 0; i < 40; i++)
        {
            mixer.CreateBus($"Extra{i}", AudioBusNames.Master);
        }

        using var rig = new Rig(mixer);
        rig.Pump(2);
        rig.Model.Update(Frame);

        var last = rig.Bus("Extra39");

        Assert.False(last.HasLevel);
        Assert.Equal("n/a", last.PeakText);
        Assert.True(rig.Bus("Extra0").HasLevel);
        Assert.Equal(mixer.Buses.Count, rig.Model.Buses.Length);
    }

    [Fact]
    public void ASumAboveFullScale_IsCountedAsOversOfTheOutput()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 26214, 200000);
        rig.PlayConstant(AudioBusNames.Sfx, 26214, 200000);
        rig.Pump(4);
        rig.Model.Update(Frame);

        var output = rig.Model.Output!;

        // Master before the limiter is 1.6 (+4 dBFS); the output after the hard clip is full scale.
        Assert.True(rig.Bus(AudioBusNames.Master).PeakDb > 3.5f);
        Assert.Equal(0f, output.PeakDb, 0.05f);
        Assert.True(output.OversTotal > 0);
        Assert.True(output.IsOverLit);
        Assert.False(string.IsNullOrEmpty(output.OversText));
        Assert.Contains($"output overs {output.OversTotal.ToString(CultureInfo.InvariantCulture)}", rig.Model.StatLines[5]);

        // The panel owns its cursor: a block is integrated once, a later read with no new block counts nothing.
        var overs = output.OversTotal;
        rig.Model.Update(Frame);
        rig.Model.Update(Frame);
        Assert.Equal(overs, output.OversTotal);

        rig.Pump(1);
        rig.Model.Update(Frame);
        Assert.True(output.OversTotal > overs);
    }

    [Fact]
    public void ReaderThatWaitedTooLong_CountsTheBlocksItMissed()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 480 * 200);
        rig.Pump(1);
        rig.Model.Update(Frame);

        rig.Pump(100);
        rig.Model.Update(Frame);
        rig.Model.RefreshText();

        Assert.InRange(rig.Model.MissedBlockCount, 1, 100);
        Assert.Contains("missed blocks " + rig.Model.MissedBlockCount.ToString(CultureInfo.InvariantCulture), rig.Model.StatLines[5]);
    }

    [Fact]
    public void Reset_GoesBackToSilence()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 480 * 200);
        rig.Pump(2);
        rig.Model.Update(Frame);
        Assert.True(rig.Bus(AudioBusNames.Sfx).HoldDb > AudioMeterScale.FloorDb);

        rig.Pump(100);
        rig.Model.Update(Frame);
        Assert.True(rig.Model.MissedBlockCount > 0);

        rig.Model.Reset();

        Assert.Equal(AudioMeterScale.FloorDb, rig.Bus(AudioBusNames.Sfx).HoldDb);
        Assert.Equal(AudioMeterScale.FloorDb, rig.Model.Output!.HoldDb);
        Assert.Equal(0, rig.Model.MissedBlockCount);
        Assert.Equal(0, rig.Model.Output.OversTotal);

        // The texts are rebuilt at the next update, without waiting for the interval.
        Assert.True(rig.Model.Update(Frame));
    }

    // ───────────────────────── metering without allocation ─────────────────────────

    [Fact]
    public void ReadingTheMeters_AllocatesNothing()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 480 * 700);
        rig.PlayConstant(AudioBusNames.Music, 8192, 480 * 700);

        // Warm up: builds the meters, the texts, and compiles the code.
        for (var i = 0; i < 5; i++)
        {
            rig.Pump(1);
            rig.Model.Update(Frame);
        }

        long allocated = 0;
        for (var i = 0; i < 30; i++)
        {
            rig.Pump(1);

            var before = AllocationWindow.Start();
            rig.Model.ReadMeters(Frame);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void AnUpdate_WhereNothingChanged_AllocatesNothing()
    {
        using var rig = new Rig();
        rig.PlayConstant(AudioBusNames.Sfx, 16384, 480 * 700);

        for (var i = 0; i < 10; i++)
        {
            rig.Pump(1);
            rig.Model.Update(Frame);
        }

        long allocated = 0;
        for (var i = 0; i < 30; i++)
        {
            rig.Pump(1);

            var before = AllocationWindow.Start();
            rig.Model.Update(Frame);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, allocated);
    }

    // ───────────────────────── a backend without the metering capability ─────────────────────────

    [Fact]
    public void WithoutTheCapability_TheModelSaysMeteringIsUnavailable_AndShowsTheCommonStatisticsOnly()
    {
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);

        try
        {
            using var service = new AudioService(new FakeAudioBackend());
            var model = new AudioProfilerModel(() => service);

            for (var i = 0; i < 10; i++)
            {
                model.Update(Frame);
            }

            var lines = model.StatLines;

            Assert.False(model.IsMeteringAvailable);
            Assert.Empty(model.Buses);
            Assert.Null(model.Output);
            Assert.StartsWith("Backend: FakeAudioBackend", lines[0]);
            Assert.Equal("Voices: 0 active, 0 refused", lines[1]);
            Assert.Equal(string.Empty, lines[2]);
            Assert.Equal(string.Empty, lines[3]);
            Assert.Equal("SPU: not hosted by this backend", lines[4]);
            Assert.StartsWith("Metering unavailable", lines[5]);
        }
        finally
        {
            Logs.Close();
        }

        // The panel asks the service whether it can meter; it never calls the metering reads that log a warning.
        Assert.DoesNotContain(logger.Warnings, warning => warning.Contains("meters"));
    }

    [Fact]
    public void WithoutAnyService_ThePanelSaysSo()
    {
        var model = new AudioProfilerModel(() => null);

        Assert.True(model.Update(Frame));
        Assert.Equal("Audio service: not available", model.StatLines[0]);
        Assert.False(model.IsMeteringAvailable);
        Assert.Empty(model.Buses);
    }

    // ───────────────────────── the panel itself, without a GPU ─────────────────────────

    [Fact]
    public void ThePanel_BuildsItsContent_AndFollowsTheModel()
    {
        using var rig = new Rig();
        var harness = ContentBrowserViewTestHarness.Create(420, 320);
        var panel = new AudioProfilerPanel(harness.Window, () => rig.Service);

        var content = panel.CreateContent();
        harness.Window.SetContent(content);

        Assert.Same(content, panel.CreateContent());
        Assert.Equal(rig.Service.Mixer.Buses.Count, panel.Model.Buses.Length);

        rig.PlayConstant(AudioBusNames.Sfx, 16384, 200000);
        var elapsedMs = 0;
        for (var i = 0; i < 40; i++)
        {
            rig.Pump(1);
            panel.Update(Frame);
            harness.AdvanceFrame(elapsedMs += 16);
        }

        Assert.Equal(-6.0206f, panel.Model.Output!.PeakDb, 1e-3f);

        // A bus added to the mixer gets a row without rebuilding anything else.
        rig.Service.Mixer.CreateBus("Ambience", AudioBusNames.Music);
        panel.Update(Frame);
        harness.AdvanceFrame(elapsedMs + 16);

        Assert.Equal(rig.Service.Mixer.Buses.Count, panel.Model.Buses.Length);

        panel.Reset();
        panel.Update(Frame);
    }

    [Fact]
    public void ThePanel_UnderABackendWithoutMetering_BuildsAndShowsTheCommonStatistics()
    {
        using var service = new AudioService(new FakeAudioBackend());
        var harness = ContentBrowserViewTestHarness.Create(420, 320);
        var panel = new AudioProfilerPanel(harness.Window, () => service);

        harness.Window.SetContent(panel.CreateContent());

        for (var i = 0; i < 5; i++)
        {
            panel.Update(Frame);
            harness.AdvanceFrame(16 * (i + 1));
        }

        Assert.False(panel.Model.IsMeteringAvailable);
        Assert.Empty(panel.Model.Buses);
        Assert.StartsWith("Metering unavailable", panel.Model.StatLines[5]);
    }
}
