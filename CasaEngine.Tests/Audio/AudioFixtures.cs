namespace CasaEngine.Tests.Audio;

/// <summary>Locates the files of Audio/Fixtures, copied next to the test assembly.</summary>
internal static class AudioFixtures
{
    public const string MonoOgg44100 = "sine-440hz-mono-44100.ogg";
    public const string StereoOgg44100 = "sine-440hz-left-880hz-right-stereo-44100.ogg";
    public const string MonoOgg22050 = "sine-440hz-mono-22050.ogg";

    public static string GetPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Audio", "Fixtures", fileName);
    }

    public static byte[] ReadBytes(string fileName)
    {
        return File.ReadAllBytes(GetPath(fileName));
    }

    /// <summary>Frequency of one channel, from its positive-going zero crossings.</summary>
    public static double MeasureFrequency(short[] samples, int channelCount, int channel, int sampleRate)
    {
        var crossings = 0;
        var firstCrossing = -1;
        var lastCrossing = -1;
        var previous = samples[channel];

        for (var frame = 1; frame < samples.Length / channelCount; frame++)
        {
            var current = samples[frame * channelCount + channel];
            if (previous < 0 && current >= 0)
            {
                crossings++;
                firstCrossing = firstCrossing < 0 ? frame : firstCrossing;
                lastCrossing = frame;
            }

            previous = current;
        }

        // N crossings delimit N-1 whole periods.
        return (crossings - 1) * (double)sampleRate / (lastCrossing - firstCrossing);
    }
}
