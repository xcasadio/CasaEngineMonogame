using CasaEngine.Editor.Controls;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Tests.Audio;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The level of the output over time (plan T10.8, decisions D18 and P55): the ring of columns (<see cref="AudioLevelHistory"/>),
/// its filling by <see cref="AudioMeterTrack.Integrate"/> (one column per elapsed column time, a short peak never lost), the strip
/// that draws it (<see cref="AudioEnvelopeControl"/>, measured on the headless desktop; its drawing needs a GPU and is for the
/// author to see) and its place in the mixer panel. The panel runs on the real software backend driven block by block.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioLevelHistoryTests
{
    private const int Block = 480;
    private const float Frame = 0.016f;
    private const float Column = 1f / 24f;

    private static AudioLevel Level(float peak, float rms = 0f) => new(peak, peak, rms, 0);

    /// <summary>What a column must hold for an amplitude: the scale of the meters, converted once.</summary>
    private static float Fraction(float amplitude) => AudioMeterControl.ToFraction(AudioMeterScale.ToDb(amplitude));

    private static AudioMeterTrack OutputWithHistory(int columns = AudioLevelHistory.DefaultColumns, float secondsPerColumn = Column)
    {
        var track = new AudioMeterTrack("Output", 0, -1, isOutput: true);
        track.EnableHistory(columns, secondsPerColumn);
        return track;
    }

    private static void Silence(AudioMeterTrack track, float seconds, float step = Frame)
    {
        for (float done = 0f; done < seconds; done += step)
        {
            track.Integrate(default, step);
        }
    }

    /// <summary>The oldest-first peak fractions the history holds.</summary>
    private static List<float> Peaks(AudioLevelHistory history)
    {
        var peaks = new List<float>();
        for (int i = 0; i < history.Count; i++)
        {
            peaks.Add(history.GetPeakFraction(i));
        }

        return peaks;
    }

    // ───────────────────────── the ring ─────────────────────────

    [Fact]
    public void TheDefaults_AreTwoHundredFortyColumns_OfTenSeconds()
    {
        var history = new AudioLevelHistory();

        Assert.Equal(240, history.Capacity);
        Assert.Equal(240, AudioLevelHistory.DefaultColumns);
        Assert.Equal(1f / 24f, history.SecondsPerColumn);
        Assert.Equal(10f, history.TotalSeconds, 1e-4f);
        Assert.Equal(0, history.Count);
    }

    [Theory]
    [InlineData(0, 0.1f)]
    [InlineData(-3, 0.1f)]
    [InlineData(4, 0f)]
    [InlineData(4, -1f)]
    [InlineData(4, float.NaN)]
    [InlineData(4, float.PositiveInfinity)]
    public void InvalidArguments_AreRefused(int columns, float secondsPerColumn)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioLevelHistory(columns, secondsPerColumn));
    }

    [Fact]
    public void TheRing_FillsInOrder_FromTheOldestToTheNewest()
    {
        var history = new AudioLevelHistory(4, 0.1f);

        history.Write(0.1f, 0.05f);
        history.Write(0.2f, 0.1f);
        history.Write(0.4f, 0.2f);

        Assert.Equal(3, history.Count);
        Assert.Equal(new[] { Fraction(0.1f), Fraction(0.2f), Fraction(0.4f) }, Peaks(history));
        Assert.Equal(Fraction(0.05f), history.GetRmsFraction(0));
        Assert.Equal(Fraction(0.2f), history.GetRmsFraction(2));
    }

    [Fact]
    public void TheRing_WhenFull_DropsTheOldestColumn_AndKeepsTheOrderAcrossSeveralWraps()
    {
        var history = new AudioLevelHistory(4, 0.1f);
        var amplitudes = new[] { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.06f, 0.07f, 0.08f, 0.09f, 0.1f };

        for (int i = 0; i < amplitudes.Length; i++)
        {
            history.Write(amplitudes[i], amplitudes[i] / 2f);

            int held = Math.Min(i + 1, 4);
            Assert.Equal(held, history.Count);

            for (int column = 0; column < held; column++)
            {
                // The column `column` of the held ones is the write number i - held + 1 + column.
                float expected = amplitudes[i - held + 1 + column];
                Assert.Equal(Fraction(expected), history.GetPeakFraction(column));
                Assert.Equal(Fraction(expected / 2f), history.GetRmsFraction(column));
            }
        }
    }

    [Fact]
    public void AnIndexOutsideTheHeldColumns_IsRefused()
    {
        var history = new AudioLevelHistory(4, 0.1f);
        history.Write(0.5f, 0.2f);

        Assert.Throws<ArgumentOutOfRangeException>(() => history.GetPeakFraction(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.GetPeakFraction(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.GetRmsFraction(1));
    }

    [Fact]
    public void Reset_ForgetsEveryColumn_AndTheRingIsUsableAgain()
    {
        var history = new AudioLevelHistory(4, 0.1f);

        for (int i = 0; i < 7; i++)
        {
            history.Write(0.5f, 0.25f);
        }

        history.Reset();

        Assert.Equal(0, history.Count);
        Assert.Equal(4, history.Capacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.GetPeakFraction(0));

        history.Write(0.2f, 0.1f);
        history.Write(0.3f, 0.1f);

        Assert.Equal(new[] { Fraction(0.2f), Fraction(0.3f) }, Peaks(history));
    }

    // ───────────────────────── the scale ─────────────────────────

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0.25f)]
    [InlineData(0.1f)]
    [InlineData(0.01f)]
    [InlineData(0.0011f)]
    [InlineData(0.0009f)]
    [InlineData(2f)]
    public void AColumn_IsOnTheScaleOfTheMeters(float amplitude)
    {
        var history = new AudioLevelHistory(4, 0.1f);

        history.Write(amplitude, amplitude);

        float expected = AudioMeterControl.ToFraction(20f * MathF.Log10(amplitude));
        Assert.Equal(expected, history.GetPeakFraction(0), 1e-5f);
        Assert.Equal(expected, history.GetRmsFraction(0), 1e-5f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    public void Silence_IsFractionZero(float amplitude)
    {
        var history = new AudioLevelHistory(4, 0.1f);

        history.Write(amplitude, amplitude);

        Assert.Equal(0f, history.GetPeakFraction(0));
        Assert.Equal(0f, history.GetRmsFraction(0));
    }

    [Fact]
    public void FullScaleAndAbove_AreFractionOne()
    {
        var history = new AudioLevelHistory(4, 0.1f);

        history.Write(1f, 1f);
        history.Write(4f, float.PositiveInfinity);

        Assert.Equal(1f, history.GetPeakFraction(0));
        Assert.Equal(1f, history.GetPeakFraction(1));
        Assert.Equal(1f, history.GetRmsFraction(1));
    }

    // ───────────────────────── the source of the strip ─────────────────────────

    [Fact]
    public void AsASource_TheRingKeepsItsColumnCount_AndTheNewestColumnIsAtTheRightEdge()
    {
        var history = new AudioLevelHistory(8, 0.1f);
        IAudioEnvelopeSource source = history;

        history.Write(0.5f, 0.25f);
        history.Write(0.25f, 0.1f);

        Assert.Equal(8, source.ColumnCount);

        // The columns that were not written yet are silent, on the left: the time axis does not move while the ring fills up.
        for (int column = 0; column < 6; column++)
        {
            source.GetColumn(column, out float lower, out float upper, out float inner);
            Assert.Equal(0f, lower);
            Assert.Equal(0f, upper);
            Assert.Equal(0f, inner);
        }

        // Symmetric around the axis, the RMS inside.
        source.GetColumn(6, out float lower6, out float upper6, out float inner6);
        Assert.Equal(Fraction(0.5f), upper6);
        Assert.Equal(-Fraction(0.5f), lower6);
        Assert.Equal(Fraction(0.25f), inner6);

        source.GetColumn(7, out float lower7, out float upper7, out float inner7);
        Assert.Equal(Fraction(0.25f), upper7);
        Assert.Equal(-Fraction(0.25f), lower7);
        Assert.Equal(Fraction(0.1f), inner7);
    }

    [Fact]
    public void AsASource_AFullRingStartsAtTheOldestColumn()
    {
        var history = new AudioLevelHistory(4, 0.1f);
        IAudioEnvelopeSource source = history;

        for (int i = 1; i <= 6; i++)
        {
            history.Write(i * 0.1f, 0f);
        }

        // Written 0.1 .. 0.6, the ring holds 0.3 .. 0.6.
        for (int column = 0; column < 4; column++)
        {
            source.GetColumn(column, out _, out float upper, out _);
            Assert.Equal(Fraction((column + 3) * 0.1f), upper);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void AsASource_AnIndexOutsideTheRing_IsAnEmptyColumn_AndDoesNotThrow(int index)
    {
        var history = new AudioLevelHistory(4, 0.1f);
        history.Write(1f, 1f);
        IAudioEnvelopeSource source = history;

        source.GetColumn(index, out float lower, out float upper, out float inner);

        Assert.Equal(0f, lower);
        Assert.Equal(0f, upper);
        Assert.Equal(0f, inner);
    }

    // ───────────────────────── Integrate fills the history ─────────────────────────

    [Fact]
    public void ATrackWithoutHistory_HasNone_AndIntegratesAsBefore()
    {
        var plain = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);
        var withHistory = new AudioMeterTrack("Sfx", 1, 1, isOutput: false);
        withHistory.EnableHistory(AudioLevelHistory.DefaultColumns, Column);

        Assert.Null(plain.History);
        Assert.NotNull(withHistory.History);

        // The same signal on both: the history changes nothing the meter shows.
        float[] peaks = { 0.5f, 0f, 0f, 0.9f, 0.1f, 0f, 0.3f, 0f };
        foreach (float peak in peaks)
        {
            plain.Integrate(Level(peak, peak / 2f), 0.03f);
            withHistory.Integrate(Level(peak, peak / 2f), 0.03f);
        }

        Assert.Equal(plain.PeakDb, withHistory.PeakDb);
        Assert.Equal(plain.RmsDb, withHistory.RmsDb);
        Assert.Equal(plain.HoldDb, withHistory.HoldDb);
        Assert.Equal(plain.OversTotal, withHistory.OversTotal);
        Assert.Null(plain.History);
    }

    [Fact]
    public void ABusTrackOfTheAudioPanelModel_NeverHasAHistory()
    {
        using var service = new AudioService(new SoftwareAudioBackend(new OfflineAudioOutput(), 16));
        var model = new AudioProfilerModel(() => service);

        model.Update(Frame);

        Assert.NotNull(model.Output);
        Assert.Null(model.Output!.History);
        Assert.All(model.Buses, track => Assert.Null(track.History));
    }

    [Fact]
    public void OneSecondInSixteenMillisecondSteps_WritesTwentyFourColumns_ToAboutOne()
    {
        var track = OutputWithHistory();

        // 62 steps of 16 ms and one of 8 ms: one second.
        for (int i = 0; i < 62; i++)
        {
            track.Integrate(Level(0.5f, 0.25f), 0.016f);
        }

        track.Integrate(Level(0.5f, 0.25f), 0.008f);

        Assert.InRange(track.History!.Count, 23, 25);
    }

    [Fact]
    public void ARunOfOneMinute_WritesOneColumnPerColumnTime_WhateverTheStep()
    {
        foreach (float step in new[] { 0.004f, 0.016f, 0.0333f, 0.05f, 0.1f })
        {
            var track = OutputWithHistory(columns: 2000);

            for (float done = 0f; done < 60f; done += step)
            {
                track.Integrate(default, step);
            }

            // 60 s is 1440 columns of 1/24 s; the last step may overshoot a little.
            Assert.InRange(track.History!.Count, 1438, 1445);
        }
    }

    [Fact]
    public void Silence_IsFractionZero_InEveryColumn()
    {
        var track = OutputWithHistory();

        Silence(track, 1f);

        var history = track.History!;
        Assert.True(history.Count > 0);
        for (int i = 0; i < history.Count; i++)
        {
            Assert.Equal(0f, history.GetPeakFraction(i));
            Assert.Equal(0f, history.GetRmsFraction(i));
        }
    }

    [Fact]
    public void ALevel_IsOnTheSameScaleAsTheMeterBar()
    {
        var track = OutputWithHistory();

        // One step that is a whole column long: one column.
        track.Integrate(Level(0.5f, 0.25f), Column);

        var history = track.History!;
        Assert.Equal(1, history.Count);
        Assert.Equal(AudioMeterControl.ToFraction(track.PeakDb), history.GetPeakFraction(0), 1e-6f);
        Assert.Equal(AudioMeterControl.ToFraction(track.RmsDb), history.GetRmsFraction(0), 1e-6f);
        Assert.Equal(Fraction(0.5f), history.GetPeakFraction(0));
        Assert.Equal(Fraction(0.25f), history.GetRmsFraction(0));
    }

    [Theory]
    [InlineData(0.004f)]
    [InlineData(0.016f)]
    [InlineData(0.04f)]
    [InlineData(0.0417f)]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    public void AnIsolatedOneBlockPeak_IsInExactlyOneColumn_WithItsValue_WhateverTheStep(float step)
    {
        var track = OutputWithHistory();

        Silence(track, 1f, step);
        track.Integrate(Level(0.5f, 0.2f), step);
        Silence(track, 1f, step);

        var history = track.History!;
        var columns = new List<int>();
        for (int i = 0; i < history.Count; i++)
        {
            if (history.GetPeakFraction(i) > 0f)
            {
                columns.Add(i);
            }
        }

        Assert.Single(columns);
        Assert.Equal(Fraction(0.5f), history.GetPeakFraction(columns[0]));
        Assert.Equal(Fraction(0.2f), history.GetRmsFraction(columns[0]));
    }

    [Fact]
    public void TheLargestPeakAndTheLargestRms_OfTheStepsOfAColumn_AreKept()
    {
        var track = OutputWithHistory();

        // Three steps of 14 ms (42 ms, a column is 41.7 ms): the column holds the largest peak and the largest RMS of the three.
        track.Integrate(Level(0.2f, 0.1f), 0.014f);
        track.Integrate(Level(0.6f, 0.05f), 0.014f);
        track.Integrate(Level(0.3f, 0.2f), 0.014f);

        var history = track.History!;
        Assert.Equal(1, history.Count);
        Assert.Equal(Fraction(0.6f), history.GetPeakFraction(0));
        Assert.Equal(Fraction(0.2f), history.GetRmsFraction(0));
    }

    [Fact]
    public void ABigStep_WritesSeveralColumns_TheFirstOneHoldsThePeak_AndTheRemainderCarries()
    {
        var track = OutputWithHistory();

        track.Integrate(Level(0.8f, 0.3f), 0.1f);

        // 0.1 s is two columns and a little less than half of a third.
        var history = track.History!;
        Assert.Equal(2, history.Count);
        Assert.Equal(Fraction(0.8f), history.GetPeakFraction(0));
        Assert.Equal(Fraction(0.3f), history.GetRmsFraction(0));
        Assert.Equal(0f, history.GetPeakFraction(1));

        // The remainder (about 16.7 ms) is not lost: 30 ms more complete a third column, 15 ms would not.
        track.Integrate(default, 0.015f);
        Assert.Equal(2, history.Count);
        track.Integrate(default, 0.015f);
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void ABigStep_DoesNotLoseThePeak_WhateverItsLength()
    {
        foreach (float seconds in new[] { 0.09f, 0.5f, 3f, 9.9f, 10f, 100f, 1e9f })
        {
            var track = OutputWithHistory();

            track.Integrate(Level(0.9f, 0.4f), seconds);

            var history = track.History!;
            float largest = Peaks(history).Max();
            Assert.True(history.Count >= 1, $"{seconds} s wrote nothing");
            Assert.Equal(Fraction(0.9f), largest);
            Assert.Equal(1, Peaks(history).Count(peak => peak > 0f));
        }
    }

    [Fact]
    public void AStepLongerThanTheRing_FillsTheRing_AndTerminates()
    {
        var track = OutputWithHistory(columns: 10, secondsPerColumn: 0.1f);

        track.Integrate(Level(1f, 1f), 1e9f);

        // The ring holds one second: it is full (or one column short of it, by the rounding of the division).
        Assert.InRange(track.History!.Count, 9, 10);
    }

    [Fact]
    public void ANonFiniteOrNegativeStep_WritesNothing_AndNothingBreaks()
    {
        var track = OutputWithHistory();

        track.Integrate(Level(0.5f), -1f);
        track.Integrate(Level(0.5f), float.NaN);

        Assert.Equal(0, track.History!.Count);

        track.Integrate(Level(0.5f), Column);
        Assert.Equal(1, track.History.Count);
    }

    [Fact]
    public void Reset_ClearsTheHistory_AndWhatWasWaitingForTheNextColumn()
    {
        var track = OutputWithHistory();
        track.Integrate(Level(0.9f, 0.5f), 0.1f);
        Assert.True(track.History!.Count > 0);

        // A peak that is still waiting for its column (10 ms of a 41.7 ms column).
        track.Integrate(Level(0.9f, 0.5f), 0.01f);

        track.Reset();

        Assert.NotNull(track.History);
        Assert.Equal(0, track.History.Count);

        track.Integrate(default, Column);
        Assert.Equal(1, track.History.Count);
        Assert.Equal(0f, track.History.GetPeakFraction(0));
    }

    [Fact]
    public void EnablingTheHistoryAgain_StartsAnEmptyOne()
    {
        var track = OutputWithHistory();
        Silence(track, 0.5f);
        var first = track.History;
        Assert.True(first!.Count > 0);

        track.EnableHistory(60, 0.5f);

        Assert.NotSame(first, track.History);
        Assert.Equal(0, track.History!.Count);
        Assert.Equal(60, track.History.Capacity);
        Assert.Equal(0.5f, track.History.SecondsPerColumn);
    }

    // ───────────────────────── no allocation ─────────────────────────

    [Fact]
    public void Write_AndReset_AllocateNothing()
    {
        var history = new AudioLevelHistory();
        for (int i = 0; i < 300; i++)
        {
            history.Write(0.5f, 0.25f);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            history.Write(0.001f * i, 0.0005f * i);
            history.GetPeakFraction(0);
            history.GetColumn(i % 240, out _, out _, out _);

            if (i % 300 == 299)
            {
                history.Reset();
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Integrate_WithAHistory_AllocatesNothing_SmallAndBigSteps()
    {
        var track = OutputWithHistory();
        for (int i = 0; i < 300; i++)
        {
            track.Integrate(Level(0.5f, 0.2f), Frame);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 2000; i++)
        {
            float step = i % 50 == 0 ? 0.5f : Frame;
            track.Integrate(Level((i % 7) * 0.1f, (i % 5) * 0.05f), step);
        }

        track.Reset();
        track.Integrate(Level(1f, 1f), 1e9f);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // ───────────────────────── the strip ─────────────────────────

    [Fact]
    public void TheStrip_BuildsAndMeasures_WithoutAGpu_AtAFixedHeight_AndStretchesItsWidth()
    {
        var harness = ContentBrowserViewTestHarness.Create(420, 200);
        var history = new AudioLevelHistory();
        var strip = new AudioEnvelopeControl(harness.Window, history);

        harness.Window.SetContent(strip);
        harness.AdvanceFrame(16);
        harness.AdvanceFrame(32);

        Assert.Same(history, strip.Source);
        Assert.False(strip.IsHitTestVisible);
        Assert.True(strip.ShowLevelMarks);
        Assert.Equal(AudioEnvelopeControl.EnvelopeHeight, strip.ActualHeight);
        Assert.Equal(48, AudioEnvelopeControl.EnvelopeHeight);
        Assert.Equal(420, strip.ActualWidth);

        // A strip for a file in the inspector: another source, no marks, and none at all.
        strip.Source = null;
        strip.ShowLevelMarks = false;
        harness.AdvanceFrame(48);

        Assert.Null(strip.Source);
        Assert.False(strip.ShowLevelMarks);
        Assert.Equal(AudioEnvelopeControl.EnvelopeHeight, strip.ActualHeight);
    }

    private sealed class FixedSource : IAudioEnvelopeSource
    {
        private readonly (float Lower, float Upper, float Inner)[] _columns;

        public FixedSource(params (float Lower, float Upper, float Inner)[] columns) => _columns = columns;

        public int ColumnCount => _columns.Length;

        public void GetColumn(int index, out float lower, out float upper, out float inner)
        {
            if ((uint)index >= (uint)_columns.Length)
            {
                lower = upper = inner = 0f;
                return;
            }

            (lower, upper, inner) = _columns[index];
        }
    }

    [Fact]
    public void ManyColumnsInOnePixel_KeepTheExtremeOfEachKind_SoAShortPeakIsNeverLost()
    {
        var source = new FixedSource(
            (-0.1f, 0.2f, 0.05f),
            (-0.9f, 0.1f, 0f),
            (-0.2f, 0.7f, 0.3f),
            (0f, 0f, 0f));

        AudioEnvelopeControl.MergeColumns(source, 0, 4, out float lower, out float upper, out float inner);

        Assert.Equal(-0.9f, lower);
        Assert.Equal(0.7f, upper);
        Assert.Equal(0.3f, inner);

        AudioEnvelopeControl.MergeColumns(source, 3, 4, out lower, out upper, out inner);
        Assert.Equal((0f, 0f, 0f), (lower, upper, inner));

        AudioEnvelopeControl.MergeColumns(source, 2, 2, out lower, out upper, out inner);
        Assert.Equal((0f, 0f, 0f), (lower, upper, inner));
    }

    [Fact]
    public void AColumnOutsideItsRange_OrNotANumber_IsClampedOrIgnored()
    {
        var source = new FixedSource(
            (-3f, 5f, 2f),
            (float.NaN, float.NaN, float.NaN));

        AudioEnvelopeControl.MergeColumns(source, 0, 2, out float lower, out float upper, out float inner);

        Assert.Equal(-1f, lower);
        Assert.Equal(1f, upper);
        Assert.Equal(1f, inner);

        AudioEnvelopeControl.MergeColumns(source, 1, 2, out lower, out upper, out inner);
        Assert.Equal((0f, 0f, 0f), (lower, upper, inner));
    }

    [Theory]
    [InlineData(240, 100)]
    [InlineData(240, 240)]
    [InlineData(240, 7)]
    [InlineData(512, 333)]
    [InlineData(10, 1)]
    public void TheSlices_CoverEveryColumn_ExactlyOnce_WithAtLeastOneColumnEach(int columns, int slices)
    {
        int next = 0;
        for (int slice = 0; slice < slices; slice++)
        {
            int start = AudioEnvelopeControl.SliceStart(slice, columns, slices);
            int end = AudioEnvelopeControl.SliceStart(slice + 1, columns, slices);

            Assert.Equal(next, start);
            Assert.True(end > start, $"slice {slice} of {slices} is empty");
            next = end;
        }

        Assert.Equal(columns, next);
    }

    // ───────────────────────── the mixer panel ─────────────────────────

    private sealed class PanelRig : IDisposable
    {
        public PanelRig(bool software = true)
        {
            Output = software ? new OfflineAudioOutput() : null;
            Service = new AudioService(software ? new SoftwareAudioBackend(Output!, 16) : new FakeAudioBackend());
            Service.MasterLimiter.IsEnabled = false;
            Harness = ContentBrowserViewTestHarness.Create(900, 700);
            Panel = new AudioMixerPanel(Harness.Window, () => Service);
            Content = Panel.CreateContent();
            Harness.Window.SetContent(Content);
        }

        public OfflineAudioOutput Output { get; }

        public AudioService Service { get; }

        public ContentBrowserViewTestHarness Harness { get; }

        public AudioMixerPanel Panel { get; }

        public MGElement Content { get; }

        public AudioEnvelopeControl Strip => Content.TraverseVisualTree().OfType<AudioEnvelopeControl>().Single();

        public void PlayConstant(string bus, short value, int frames)
        {
            var samples = new short[2 * frames];
            Array.Fill(samples, value);
            Service.PlayClip(new PcmAudioClip(samples, 48000, 2), bus, AudioVoiceParameters.Default);
            Service.Update(0.01f);
        }

        public void Pump(int blocks)
        {
            for (int i = 0; i < blocks; i++)
            {
                Output.Pump(Block);
            }
        }

        public void Dispose()
        {
            Panel.Dispose();
            Service.Dispose();
        }
    }

    [Fact]
    public void ThePanel_ShowsTheBand_UnderTheToolbar_WithItsTitle()
    {
        using var rig = new PanelRig();

        var everything = rig.Content.TraverseVisualTree().ToList();
        var apply = everything.OfType<MGButton>().Single(button => button.TraverseVisualTree().OfType<MGTextBlock>().Any(text => text.Text == "Apply to live mixer"));
        var title = everything.OfType<MGTextBlock>().Single(text => text.Text == "Output level (last 10 s)");
        var strip = rig.Strip;

        Assert.True(everything.IndexOf(apply) < everything.IndexOf(title));
        Assert.True(everything.IndexOf(title) < everything.IndexOf(strip));
        Assert.True(strip.ShowLevelMarks);
        Assert.False(strip.IsHitTestVisible);
        Assert.Equal(AudioLevelHistory.DefaultColumns * AudioLevelHistory.DefaultSecondsPerColumn, 10f, 1e-4f);

        rig.Harness.AdvanceFrame(16);
        rig.Harness.AdvanceFrame(32);

        Assert.Equal(AudioEnvelopeControl.EnvelopeHeight, strip.ActualHeight);
        Assert.True(strip.ActualWidth > AudioEnvelopeControl.PreferredEnvelopeWidth);
    }

    [Fact]
    public void ThePanel_EnablesTheHistoryOfItsOwnOutputTrack_AndTheStripDrawsIt()
    {
        using var rig = new PanelRig();

        Assert.Null(rig.Strip.Source);

        rig.Panel.Update(Frame);

        var output = rig.Panel.MeterModel.Output!;
        Assert.NotNull(output.History);
        Assert.Equal(AudioLevelHistory.DefaultColumns, output.History!.Capacity);
        Assert.Equal(AudioLevelHistory.DefaultSecondsPerColumn, output.History.SecondsPerColumn);
        Assert.Same(output.History, rig.Strip.Source);

        // Only the output of the panel's model: no bus keeps one.
        Assert.All(rig.Panel.MeterModel.Buses, track => Assert.Null(track.History));
    }

    [Fact]
    public void ThePanel_FollowsTheLevelOfTheOutput_AnIsolatedClickLeavesAClearTrace_AndResetMetersClearsIt()
    {
        using var rig = new PanelRig();
        rig.Panel.Update(Frame);

        // A second of silence, one block of 0.5 on Sfx, then a second of silence, one editor frame per block.
        for (int i = 0; i < 60; i++)
        {
            rig.Pump(1);
            rig.Panel.Update(Frame);
        }

        rig.PlayConstant("Sfx", 16384, Block);

        for (int i = 0; i < 70; i++)
        {
            rig.Pump(1);
            rig.Panel.Update(Frame);
        }

        var history = rig.Panel.MeterModel.Output!.History!;
        var lit = Peaks(history).Where(peak => peak > 0f).ToList();

        Assert.True(history.Count >= 24);
        Assert.Single(lit);
        Assert.Equal(Fraction(0.5f), lit[0], 1e-4f);

        // The strip reads the same column: the newest on the right, the click a little to the left of it.
        IAudioEnvelopeSource source = history;
        int litColumns = 0;
        for (int column = 0; column < source.ColumnCount; column++)
        {
            source.GetColumn(column, out float lower, out float upper, out _);
            if (upper > 0f)
            {
                litColumns++;
                Assert.Equal(-upper, lower);
            }
        }

        Assert.Equal(1, litColumns);

        rig.Panel.ResetMeters();

        Assert.Equal(0, history.Count);
        Assert.Same(history, rig.Panel.MeterModel.Output.History);
        Assert.Same(history, rig.Strip.Source);
    }

    [Fact]
    public void WithoutAMeteringBackend_TheBandIsEmpty_AndNothingThrows()
    {
        using var rig = new PanelRig(software: false);

        for (int i = 0; i < 5; i++)
        {
            rig.Panel.Update(Frame);
            rig.Harness.AdvanceFrame(16 * (i + 1));
        }

        Assert.Null(rig.Panel.MeterModel.Output);
        Assert.Null(rig.Strip.Source);
        rig.Panel.ResetMeters();
    }

    [Fact]
    public void WithoutAnyService_TheBandIsEmpty_AndNothingThrows()
    {
        var harness = ContentBrowserViewTestHarness.Create(900, 700);
        var panel = new AudioMixerPanel(harness.Window, () => null);
        var content = panel.CreateContent();
        harness.Window.SetContent(content);

        panel.Update(Frame);
        panel.ResetMeters();

        Assert.Null(content.TraverseVisualTree().OfType<AudioEnvelopeControl>().Single().Source);
    }

    [Fact]
    public void ThePanelUpdate_WithTheHistory_AllocatesNothing()
    {
        using var rig = new PanelRig();
        rig.PlayConstant("Sfx", 16384, Block * 700);

        // Warm up: the model builds its meters, the history is enabled, the code compiles.
        for (int i = 0; i < 20; i++)
        {
            rig.Pump(1);
            rig.Panel.Update(Frame);
        }

        long allocated = 0;
        for (int i = 0; i < 60; i++)
        {
            rig.Pump(1);

            long before = AllocationWindow.Start();
            rig.Panel.Update(Frame);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, allocated);
        Assert.True(rig.Panel.MeterModel.Output!.History!.Count > 0);
    }
}
