using System.Runtime.InteropServices;
using CasaEngine.Core.Logging;
using CasaEngine.EditorServices.Audio;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Tests.Audio;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// The drawing of a sound file for the sound inspector (plan decisions D18 and P56): the reduction of samples to min and max
/// columns (pure), and the single path that decodes a wav or ogg file through the engine's loader under a size cap. The catalog,
/// the project path and the log are global state, hence the serialized collection and the restore in <see cref="Dispose"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class AudioWaveformBuilderTests : IDisposable
{
    private const float Tolerance = 1e-3f;

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) => Lines.Add("warning: " + msg);
        public void WriteError(string msg) => Lines.Add("error: " + msg);
    }

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath = EngineEnvironment.ProjectPath;
    private readonly CapturingLogger _logger = new();

    public AudioWaveformBuilderTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-waveform-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);
        EngineEnvironment.ProjectPath = _projectDirectory;
        AssetCatalog.ClearInternal();
        Logs.AddLogger(_logger);
    }

    public void Dispose()
    {
        Logs.Close();
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();

        try
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder left behind is harmless.
        }
    }

    private static float[] Minimums(AudioWaveform waveform) => Enumerable.Range(0, waveform.ColumnCount).Select(waveform.GetMinimum).ToArray();

    private static float[] Maximums(AudioWaveform waveform) => Enumerable.Range(0, waveform.ColumnCount).Select(waveform.GetMaximum).ToArray();

    private static byte[] Pcm16Wav(short[] samples, int sampleRate, int channelCount)
    {
        return WavBuilder.Create(WavBuilder.PcmFormatTag, sampleRate, channelCount, 16, MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
    }

    private string WriteFile(string relativePath, byte[] bytes)
    {
        string fullPath = Path.Combine(_projectDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, bytes);
        return fullPath;
    }

    private Guid Catalog(string relativePath)
    {
        var info = new AssetInfo { Name = Path.GetFileNameWithoutExtension(relativePath), FileName = relativePath };
        AssetCatalog.AddInternal(info);
        return info.Id;
    }

    // ----- BuildFromSamples -----

    [Fact]
    public void Silence_IsZeroInEveryColumn()
    {
        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(new short[4410], 1, 10, 44100);

        Assert.Equal(10, waveform.ColumnCount);
        Assert.All(Minimums(waveform), value => Assert.Equal(0f, value));
        Assert.All(Maximums(waveform), value => Assert.Equal(0f, value));
    }

    [Fact]
    public void AFullScaleSquare_ReachesPlusAndMinusOne_InEveryColumn()
    {
        // Each column holds both levels: 20 frames high, 20 frames low.
        var samples = new short[800];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = index % 40 < 20 ? short.MaxValue : short.MinValue;
        }

        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(samples, 1, 8, 8000);

        Assert.Equal(8, waveform.ColumnCount);
        Assert.All(Minimums(waveform), value => Assert.Equal(-1f, value, Tolerance));
        Assert.All(Maximums(waveform), value => Assert.Equal(1f, value, Tolerance));
    }

    [Fact]
    public void ASineWithAKnownPeakPerColumn_GivesThatPeak_ToAThousandth()
    {
        // 8 columns of 1000 frames, 10 whole periods each (100 frames per period, so the peaks are sampled exactly),
        // with a different amplitude in each column.
        const int columns = 8;
        const int framesPerColumn = 1000;
        var samples = new short[columns * framesPerColumn];
        var amplitudes = new float[columns];
        for (int column = 0; column < columns; column++)
        {
            amplitudes[column] = 0.1f + (0.1f * column);
            for (int frame = 0; frame < framesPerColumn; frame++)
            {
                double angle = 2.0 * Math.PI * frame / 100.0;
                samples[(column * framesPerColumn) + frame] = (short)Math.Round(amplitudes[column] * 32768.0 * Math.Sin(angle));
            }
        }

        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(samples, 1, columns, 44100);

        for (int column = 0; column < columns; column++)
        {
            Assert.Equal(amplitudes[column], waveform.GetMaximum(column), Tolerance);
            Assert.Equal(-amplitudes[column], waveform.GetMinimum(column), Tolerance);
        }
    }

    [Fact]
    public void AStereoClip_TakesTheExtremesOfBothChannels()
    {
        // Left constant at +0.5, right constant at -1: the column reaches both.
        var samples = new short[2 * 1000];
        for (int frame = 0; frame < 1000; frame++)
        {
            samples[2 * frame] = 16384;
            samples[(2 * frame) + 1] = short.MinValue;
        }

        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(samples, 2, 4, 44100);

        Assert.Equal(2, waveform.ChannelCount);
        Assert.All(Minimums(waveform), value => Assert.Equal(-1f, value, Tolerance));
        Assert.All(Maximums(waveform), value => Assert.Equal(0.5f, value, Tolerance));
    }

    [Fact]
    public void AStereoClip_KeepsALoudRightChannelThatTheLeftChannelDoesNotHave()
    {
        // Only the right channel of one frame is loud: it must not be mistaken for the left channel of the next frame.
        var samples = new short[2 * 100];
        samples[(2 * 30) + 1] = 30000;

        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(samples, 2, 100, 44100);

        for (int column = 0; column < 100; column++)
        {
            Assert.Equal(column == 30 ? 30000f / 32768f : 0f, waveform.GetMaximum(column));
            Assert.Equal(0f, waveform.GetMinimum(column));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AClipShorterThanTheColumnCount_GetsOneColumnPerFrame(int channelCount)
    {
        short[] frames = { 1000, -2000, 3000 };
        var samples = new short[frames.Length * channelCount];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            for (int channel = 0; channel < channelCount; channel++)
            {
                samples[(frame * channelCount) + channel] = frames[frame];
            }
        }

        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(samples, channelCount, AudioWaveformBuilder.DefaultColumnCount, 44100);

        Assert.Equal(3, waveform.ColumnCount);
        for (int column = 0; column < 3; column++)
        {
            Assert.Equal(frames[column] / 32768f, waveform.GetMinimum(column));
            Assert.Equal(frames[column] / 32768f, waveform.GetMaximum(column));
        }
    }

    [Fact]
    public void AnEmptyClip_HasNoColumn_AndNoDuration()
    {
        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(ReadOnlySpan<short>.Empty, 2, 512, 44100);

        Assert.Equal(0, waveform.ColumnCount);
        Assert.Equal(TimeSpan.Zero, waveform.Duration);
    }

    [Fact]
    public void ATrailingPartialFrame_IsIgnored()
    {
        // Two whole stereo frames and one stray sample, which would be the left channel of a third frame.
        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(new short[] { 1, 2, 3, 4, 32000 }, 2, 512, 100);

        Assert.Equal(2, waveform.ColumnCount);
        Assert.Equal(new[] { 2f / 32768f, 4f / 32768f }, Maximums(waveform));
        Assert.Equal(TimeSpan.FromSeconds(0.02), waveform.Duration);
    }

    [Theory]
    [InlineData(1000, 7)]
    [InlineData(1000, 512)]
    [InlineData(513, 512)]
    [InlineData(5000, 512)]
    public void EveryFrame_BelongsToExactlyOneColumn(int frameCount, int columns)
    {
        // A lone spike at each frame in turn: one and only one column sees it, so no frame is skipped or counted twice.
        for (int spike = 0; spike < frameCount; spike++)
        {
            var samples = new short[frameCount];
            samples[spike] = 20000;

            AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(samples, 1, columns, 44100);

            Assert.Equal(1, Maximums(waveform).Count(value => value > 0f));
        }
    }

    [Fact]
    public void TheDurationTheSampleRateAndTheChannelCount_AreThoseOfTheClip()
    {
        AudioWaveform waveform = AudioWaveformBuilder.BuildFromSamples(new short[2 * 22050], 2, 64, 11025);

        Assert.Equal(TimeSpan.FromSeconds(2), waveform.Duration);
        Assert.Equal(11025, waveform.SampleRate);
        Assert.Equal(2, waveform.ChannelCount);
    }

    [Fact]
    public void ABadCount_IsAProgrammingError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioWaveformBuilder.BuildFromSamples(new short[10], 0, 8, 44100));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioWaveformBuilder.BuildFromSamples(new short[10], 1, 0, 44100));
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioWaveformBuilder.BuildFromSamples(new short[10], 1, 8, 0));
    }

    // ----- BuildFromFile and BuildFromAsset -----

    [Fact]
    public void AWav16_IsDecodedByTheEngineLoader_AndReducedToTheDefaultColumns()
    {
        // 1 second at 8 kHz (8000 frames in 512 columns of about 15.6 frames), a sine of amplitude 0.25 and 80 frames per period:
        // no column holds a whole period, but the columns that hold a peak reach 0.25 and none goes beyond it.
        var samples = new short[8000];
        for (int frame = 0; frame < samples.Length; frame++)
        {
            samples[frame] = (short)Math.Round(0.25 * 32768.0 * Math.Sin(2.0 * Math.PI * frame / 80.0));
        }

        string path = WriteFile("Sounds/sine.wav", Pcm16Wav(samples, 8000, 1));

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromFile(path);

        Assert.Equal(AudioWaveformStatus.Ready, result.Status);
        Assert.Equal(string.Empty, result.Reason);
        AudioWaveform waveform = result.Waveform;
        Assert.Equal(AudioWaveformBuilder.DefaultColumnCount, waveform.ColumnCount);
        Assert.Equal(1, waveform.ChannelCount);
        Assert.Equal(8000, waveform.SampleRate);
        Assert.Equal(TimeSpan.FromSeconds(1), waveform.Duration);
        Assert.All(Maximums(waveform), value => Assert.InRange(value, -0.25f - Tolerance, 0.25f + Tolerance));
        Assert.All(Minimums(waveform), value => Assert.InRange(value, -0.25f - Tolerance, 0.25f + Tolerance));
        for (int column = 0; column < waveform.ColumnCount; column++)
        {
            Assert.True(waveform.GetMinimum(column) <= waveform.GetMaximum(column));
        }

        Assert.Contains(Maximums(waveform), value => Math.Abs(value - 0.25f) < Tolerance);
        Assert.Contains(Minimums(waveform), value => Math.Abs(value + 0.25f) < Tolerance);
        Assert.Empty(_logger.Lines);
    }

    [Fact]
    public void AWav24_GoesThroughTheEngineLoader()
    {
        // 24 bit little endian: 0x400000 is +0.5 of full scale and 0xC00000 is -0.5; the left channel is high and the right low.
        var data = new byte[3 * 2 * 1000];
        for (int frame = 0; frame < 1000; frame++)
        {
            int left = 6 * frame;
            data[left + 2] = 0x40;
            data[left + 5] = 0xC0;
        }

        string path = WriteFile("Sounds/pcm24.wav", WavBuilder.Create(WavBuilder.PcmFormatTag, 48000, 2, 24, data));

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromFile(path);

        Assert.Equal(AudioWaveformStatus.Ready, result.Status);
        Assert.Equal(2, result.Waveform.ChannelCount);
        Assert.Equal(48000, result.Waveform.SampleRate);
        Assert.Equal(512, result.Waveform.ColumnCount);
        Assert.All(Maximums(result.Waveform), value => Assert.Equal(0.5f, value, Tolerance));
        Assert.All(Minimums(result.Waveform), value => Assert.Equal(-0.5f, value, Tolerance));
    }

    [Theory]
    [InlineData(AudioFixtures.MonoOgg44100, 1, 44100)]
    [InlineData(AudioFixtures.StereoOgg44100, 2, 44100)]
    [InlineData(AudioFixtures.MonoOgg22050, 1, 22050)]
    public void AnOgg_GoesThroughTheEngineLoader(string fixture, int channelCount, int sampleRate)
    {
        string path = WriteFile("Music/" + fixture, AudioFixtures.ReadBytes(fixture));

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromFile(path);

        Assert.Equal(AudioWaveformStatus.Ready, result.Status);
        AudioWaveform waveform = result.Waveform;
        Assert.Equal(channelCount, waveform.ChannelCount);
        Assert.Equal(sampleRate, waveform.SampleRate);
        Assert.Equal(0.5, waveform.Duration.TotalSeconds, 0.05);
        Assert.Equal(512, waveform.ColumnCount);

        // A sine: the columns reach above and below the axis.
        Assert.True(Maximums(waveform).Max() > 0.01f);
        Assert.True(Minimums(waveform).Min() < -0.01f);
        Assert.All(Maximums(waveform), value => Assert.InRange(value, -1f, 1f));
        Assert.All(Minimums(waveform), value => Assert.InRange(value, -1f, 1f));
    }

    [Fact]
    public void BuildFromAsset_ResolvesTheFileThroughTheCatalogAndTheProjectFolder()
    {
        WriteFile("Sounds/step.wav", Pcm16Wav(new short[] { 100, -200, 300, -400 }, 44100, 1));
        Guid id = Catalog("Sounds/step.wav");

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromAsset(id);

        Assert.Equal(AudioWaveformStatus.Ready, result.Status);
        Assert.Equal(4, result.Waveform.ColumnCount);
        Assert.Equal(-400f / 32768f, result.Waveform.GetMinimum(3));
    }

    [Fact]
    public void NoFile_NotInTheCatalog_AndNotOnDisk_EachHaveTheirReason_WithoutALogLine()
    {
        Guid onlyInCatalog = Catalog("Sounds/gone.wav");

        AudioWaveformResult none = AudioWaveformBuilder.BuildFromAsset(Guid.Empty);
        AudioWaveformResult unknown = AudioWaveformBuilder.BuildFromAsset(Guid.NewGuid());
        AudioWaveformResult missing = AudioWaveformBuilder.BuildFromAsset(onlyInCatalog);

        Assert.Equal(AudioWaveformStatus.NoFile, none.Status);
        Assert.Equal("No audio file.", none.Reason);
        Assert.Equal(AudioWaveformStatus.NotFound, unknown.Status);
        Assert.Equal(AudioWaveformStatus.NotFound, missing.Status);
        Assert.Equal("Audio file not found.", missing.Reason);
        Assert.All(new[] { none, unknown, missing }, result => Assert.Null(result.Waveform));
        Assert.Empty(_logger.Lines);
    }

    [Theory]
    [InlineData("Sounds/garbage.wav")]
    [InlineData("Sounds/garbage.ogg")]
    [InlineData("Sounds/format.mp3")]
    public void AFileThatCannotBeDecoded_IsUnreadable_WithOneLogLine_AndNeverThrows(string relativePath)
    {
        WriteFile(relativePath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
        Guid id = Catalog(relativePath);

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromAsset(id);

        Assert.Equal(AudioWaveformStatus.Unreadable, result.Status);
        Assert.Equal("Audio file unreadable.", result.Reason);
        Assert.Null(result.Waveform);
        Assert.Single(_logger.Lines);
    }

    [Fact]
    public void AFileOverTheCap_IsNotDecoded_AndSaysItIsTooLarge()
    {
        // Only the length matters: the size is checked before the file is read, so this file is never decoded.
        string path = Path.Combine(_projectDirectory, "huge.wav");
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(AudioWaveformBuilder.MaxDrawableFileBytes + 1);
        }

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromFile(path);

        Assert.Equal(AudioWaveformStatus.TooLarge, result.Status);
        Assert.Null(result.Waveform);
        Assert.Contains("Too large to draw", result.Reason, StringComparison.Ordinal);
        Assert.Contains("64 MB", result.Reason, StringComparison.Ordinal);
        Assert.Empty(_logger.Lines);
    }

    [Fact]
    public void AFileOfExactlyTheCap_IsStillRead()
    {
        // Not a valid wav, so it fails to decode: but as Unreadable, which shows the size check let it through.
        string path = Path.Combine(_projectDirectory, "edge.wav");
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(AudioWaveformBuilder.MaxDrawableFileBytes);
        }

        AudioWaveformResult result = AudioWaveformBuilder.BuildFromFile(path);

        Assert.Equal(AudioWaveformStatus.Unreadable, result.Status);
    }

    [Fact]
    public void TheCap_IsSixtyFourMegabytes()
    {
        Assert.Equal(64L * 1024 * 1024, AudioWaveformBuilder.MaxDrawableFileBytes);
        Assert.Equal(512, AudioWaveformBuilder.DefaultColumnCount);
    }
}
