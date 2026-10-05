using CasaEngine.Framework.Audio.Decoding;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class OggDecoderTests
{
    private const string Source = "unit-test.ogg";

    // The fixtures are 0.5 s long. Vorbis stores the exact length in the last page granule
    // position so the decoded length is normally exact; allow one long block (2048 frames) of
    // tolerance for an encoder that does not trim.
    private const int FrameTolerance = 2048;

    [Fact]
    public void Decode_Mono44100_HasTheExpectedFormatAndFrequency()
    {
        var decoded = OggDecoder.Decode(AudioFixtures.ReadBytes(AudioFixtures.MonoOgg44100), Source);

        Assert.Equal(44100, decoded.SampleRate);
        Assert.Equal(1, decoded.ChannelCount);
        Assert.InRange(decoded.Samples.Length, 22050 - FrameTolerance, 22050 + FrameTolerance);
        Assert.InRange(AudioFixtures.MeasureFrequency(decoded.Samples, 1, 0, 44100), 438, 442);
    }

    [Fact]
    public void Decode_Stereo_KeepsEachChannelOnItsOwnFrequency()
    {
        var decoded = OggDecoder.Decode(AudioFixtures.ReadBytes(AudioFixtures.StereoOgg44100), Source);

        Assert.Equal(44100, decoded.SampleRate);
        Assert.Equal(2, decoded.ChannelCount);
        Assert.InRange(decoded.Samples.Length / 2, 22050 - FrameTolerance, 22050 + FrameTolerance);
        Assert.InRange(AudioFixtures.MeasureFrequency(decoded.Samples, 2, 0, 44100), 438, 442);
        Assert.InRange(AudioFixtures.MeasureFrequency(decoded.Samples, 2, 1, 44100), 878, 882);
    }

    [Fact]
    public void Decode_Mono22050_KeepsTheSourceRate()
    {
        var decoded = OggDecoder.Decode(AudioFixtures.ReadBytes(AudioFixtures.MonoOgg22050), Source);

        Assert.Equal(22050, decoded.SampleRate);
        Assert.Equal(1, decoded.ChannelCount);
        Assert.InRange(decoded.Samples.Length, 11025 - FrameTolerance, 11025 + FrameTolerance);
        Assert.InRange(AudioFixtures.MeasureFrequency(decoded.Samples, 1, 0, 22050), 438, 442);
    }

    [Fact]
    public void Decode_FromAStream_GivesTheSameResultAndLeavesTheStreamOpen()
    {
        var bytes = AudioFixtures.ReadBytes(AudioFixtures.MonoOgg44100);
        using var stream = new MemoryStream(bytes);

        var fromStream = OggDecoder.Decode(stream, Source);

        Assert.True(stream.CanRead);
        Assert.Equal(OggDecoder.Decode(bytes, Source).Samples, fromStream.Samples);
    }

    [Fact]
    public void Decode_CorruptBytes_ThrowsInvalidDataNamingTheSource()
    {
        var bytes = new byte[256];
        new Random(1234).NextBytes(bytes);

        var exception = Assert.Throws<InvalidDataException>(() => OggDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_ATruncatedHeader_ThrowsInvalidDataNamingTheSource()
    {
        var bytes = AudioFixtures.ReadBytes(AudioFixtures.MonoOgg44100).AsSpan(0, 40).ToArray();

        var exception = Assert.Throws<InvalidDataException>(() => OggDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_EmptyData_ThrowsInvalidDataNamingTheSource()
    {
        var exception = Assert.Throws<InvalidDataException>(() => OggDecoder.Decode(Array.Empty<byte>(), Source));

        Assert.Contains(Source, exception.Message);
    }
}
