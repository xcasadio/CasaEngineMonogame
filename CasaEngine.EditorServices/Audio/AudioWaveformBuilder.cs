using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;

namespace CasaEngine.EditorServices.Audio;

/// <summary>
/// The drawing of a sound file (plan decision D18): the lowest and the highest sample of each of a few hundred columns, over all
/// the channels, as fractions of full scale in [-1, 1]. Built once, then read.
/// </summary>
public sealed class AudioWaveform
{
    private readonly float[] _minimums;
    private readonly float[] _maximums;

    /// <param name="minimums">The lowest sample of each column, in [-1, 1]. Not copied.</param>
    /// <param name="maximums">The highest sample of each column, in [-1, 1], as many as <paramref name="minimums"/>. Not copied.</param>
    public AudioWaveform(float[] minimums, float[] maximums, TimeSpan duration, int sampleRate, int channelCount)
    {
        ArgumentNullException.ThrowIfNull(minimums);
        ArgumentNullException.ThrowIfNull(maximums);

        if (minimums.Length != maximums.Length)
        {
            throw new ArgumentException("There must be as many maximums as minimums.", nameof(maximums));
        }

        _minimums = minimums;
        _maximums = maximums;
        Duration = duration;
        SampleRate = sampleRate;
        ChannelCount = channelCount;
    }

    public int ColumnCount => _minimums.Length;

    public TimeSpan Duration { get; }

    /// <summary>Sample rate of the file, in Hz.</summary>
    public int SampleRate { get; }

    /// <summary>1 for mono, 2 for stereo.</summary>
    public int ChannelCount { get; }

    /// <summary>The lowest sample of column <paramref name="index"/>, in [-1, 1].</summary>
    public float GetMinimum(int index) => _minimums[index];

    /// <summary>The highest sample of column <paramref name="index"/>, in [-1, 1].</summary>
    public float GetMaximum(int index) => _maximums[index];
}

/// <summary>Why a file has no drawing, or <see cref="Ready"/> when it has one.</summary>
public enum AudioWaveformStatus
{
    Ready,
    NoFile,
    NotFound,
    Unreadable,
    TooLarge,
}

/// <summary>The outcome of <see cref="AudioWaveformBuilder.BuildFromAsset"/> and <see cref="AudioWaveformBuilder.BuildFromFile"/>.</summary>
/// <param name="Waveform">The drawing, null unless <paramref name="Status"/> is <see cref="AudioWaveformStatus.Ready"/>.</param>
/// <param name="Status">Whether there is a drawing, and if not why.</param>
/// <param name="Reason">What to tell the author when there is no drawing, empty when there is one.</param>
public sealed record AudioWaveformResult(AudioWaveform Waveform, AudioWaveformStatus Status, string Reason);

/// <summary>
/// Builds the drawing of a sound file for the sound inspector (plan decisions D18 and P56): a single path, the file is decoded
/// transiently by <see cref="AudioClipLoader"/> (so wav and ogg, as the engine reads them), reduced to
/// <see cref="DefaultColumnCount"/> min and max columns, and the decoded clip is released at once. Files over
/// <see cref="MaxDrawableFileBytes"/> are not decoded. The cost is the whole decode of the file on the calling thread: call it
/// when a sound is opened or its file changed, never per frame.
/// </summary>
public static class AudioWaveformBuilder
{
    /// <summary>Number of columns the inspector draws.</summary>
    public const int DefaultColumnCount = 512;

    /// <summary>Largest file, in bytes, that is decoded to be drawn (64 MB). A file of exactly this size is drawn.</summary>
    public const long MaxDrawableFileBytes = 64L * 1024 * 1024;

    private const float InverseShortRange = 1f / 32768f;

    /// <summary>
    /// Reduces interleaved 16 bit samples to <paramref name="columns"/> columns, each holding the lowest and the highest sample of
    /// its share of the frames over all the channels. A clip with fewer frames than <paramref name="columns"/> gets one column per
    /// frame, and an empty clip none. A trailing partial frame is ignored.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A count is less than 1.</exception>
    public static AudioWaveform BuildFromSamples(ReadOnlySpan<short> interleaved, int channelCount, int columns, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channelCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);

        int frames = interleaved.Length / channelCount;
        int columnCount = Math.Min(columns, frames);
        var minimums = new float[columnCount];
        var maximums = new float[columnCount];

        for (int column = 0; column < columnCount; column++)
        {
            // At least one frame per column, since there are no more columns than frames.
            int firstFrame = (int)((long)column * frames / columnCount);
            int endFrame = (int)((long)(column + 1) * frames / columnCount);
            ReadOnlySpan<short> share = interleaved.Slice(firstFrame * channelCount, (endFrame - firstFrame) * channelCount);

            short lowest = short.MaxValue;
            short highest = short.MinValue;
            for (int index = 0; index < share.Length; index++)
            {
                short sample = share[index];
                if (sample < lowest)
                {
                    lowest = sample;
                }

                if (sample > highest)
                {
                    highest = sample;
                }
            }

            minimums[column] = lowest * InverseShortRange;
            maximums[column] = highest * InverseShortRange;
        }

        return new AudioWaveform(minimums, maximums, TimeSpan.FromSeconds((double)frames / sampleRate), sampleRate, channelCount);
    }

    /// <summary>The drawing of the audio file of the catalog asset <paramref name="audioFileAssetId"/>, read from the project folder.</summary>
    public static AudioWaveformResult BuildFromAsset(Guid audioFileAssetId, int columns = DefaultColumnCount)
    {
        if (audioFileAssetId == Guid.Empty)
        {
            return Failure(AudioWaveformStatus.NoFile, "No audio file.");
        }

        AssetInfo info = AssetCatalog.Get(audioFileAssetId);
        if (info == null || string.IsNullOrWhiteSpace(info.FileName))
        {
            return Failure(AudioWaveformStatus.NotFound, "Audio file not found.");
        }

        return BuildFromFile(Path.Combine(EngineEnvironment.ProjectPath, info.FileName), columns);
    }

    /// <summary>
    /// The drawing of a wav or ogg file. Never throws: a file that is missing, too large (<see cref="MaxDrawableFileBytes"/>) or
    /// that the engine cannot decode (the decoder logs the cause) gives a result without a drawing and with the reason.
    /// </summary>
    public static AudioWaveformResult BuildFromFile(string fullPath, int columns = DefaultColumnCount)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return Failure(AudioWaveformStatus.NotFound, "Audio file not found.");
        }

        long length;
        try
        {
            length = new FileInfo(fullPath).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(AudioWaveformStatus.Unreadable, "Audio file unreadable.");
        }

        if (length > MaxDrawableFileBytes)
        {
            return Failure(
                AudioWaveformStatus.TooLarge,
                $"Too large to draw ({length / (1024 * 1024)} MB, the limit is {MaxDrawableFileBytes / (1024 * 1024)} MB).");
        }

        if (new AudioClipLoader().LoadAsset(fullPath, null) is not PcmAudioClip clip)
        {
            return Failure(AudioWaveformStatus.Unreadable, "Audio file unreadable.");
        }

        try
        {
            AudioWaveform waveform = BuildFromSamples(clip.Samples.Span, clip.ChannelCount, columns, clip.SampleRate);
            return new AudioWaveformResult(waveform, AudioWaveformStatus.Ready, string.Empty);
        }
        finally
        {
            // The decoded samples are not kept: only the columns are.
            clip.Dispose();
        }
    }

    private static AudioWaveformResult Failure(AudioWaveformStatus status, string reason)
    {
        return new AudioWaveformResult(null, status, reason);
    }
}
