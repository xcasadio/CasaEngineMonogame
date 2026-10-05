using System.Text;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Decoding;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Microsoft ADPCM and IMA ADPCM wav files, compared sample for sample with the 16 bit PCM that ffmpeg
/// decodes from the very same files (commands in Fixtures/README.md).
/// </summary>
public class AdpcmDecoderTests
{
    private const string Source = "adpcm-test.wav";

    // 0.4 s at 22050 Hz, the value of the 'fact' chunk written by ffmpeg.
    private const int FactFrames = 8820;

    public static TheoryData<string, int> Fixtures => new()
    {
        { "adpcm-ms-mono-22050", 1 },
        { "adpcm-ms-stereo-22050", 2 },
        { "adpcm-ima-mono-22050", 1 },
        { "adpcm-ima-stereo-22050", 2 },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Decode_MatchesTheFfmpegReferenceSampleForSample(string name, int channelCount)
    {
        var reference = WavDecoder.Decode(AudioFixtures.ReadBytes(name + ".reference.wav"), name + ".reference.wav");

        var decoded = WavDecoder.Decode(AudioFixtures.ReadBytes(name + ".wav"), name + ".wav");

        Assert.Equal(22050, decoded.SampleRate);
        Assert.Equal(channelCount, decoded.ChannelCount);
        Assert.Equal(FactFrames * channelCount, decoded.Samples.Length);
        // ffmpeg keeps the padding of the last block, the 'fact' chunk trims it: the audio is the same prefix.
        Assert.True(reference.Samples.Length >= decoded.Samples.Length);
        Assert.Equal(reference.Samples.AsSpan(0, decoded.Samples.Length).ToArray(), decoded.Samples);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Decode_WithoutAFactChunk_DecodesWholeBlocksLikeFfmpeg(string name, int channelCount)
    {
        var reference = WavDecoder.Decode(AudioFixtures.ReadBytes(name + ".reference.wav"), name + ".reference.wav");
        var bytes = AudioFixtures.ReadBytes(name + ".wav");
        RenameChunk(bytes, "fact", "junk");

        var decoded = WavDecoder.Decode(bytes, Source);

        Assert.Equal(channelCount, decoded.ChannelCount);
        Assert.Equal(reference.Samples, decoded.Samples);
        Assert.True(decoded.Samples.Length > FactFrames * channelCount);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Decode_ProducesASineAtTheRightFrequency(string name, int channelCount)
    {
        var decoded = WavDecoder.Decode(AudioFixtures.ReadBytes(name + ".wav"), name + ".wav");

        Assert.InRange(AudioFixtures.MeasureFrequency(decoded.Samples, channelCount, 0, decoded.SampleRate), 435, 445);
        if (channelCount == 2)
        {
            Assert.InRange(AudioFixtures.MeasureFrequency(decoded.Samples, channelCount, 1, decoded.SampleRate), 870, 890);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Decode_TruncatedFile_Throws(string name, int channelCount)
    {
        var bytes = AudioFixtures.ReadBytes(name + ".wav");

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes.AsSpan(0, bytes.Length - 10), Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("truncated", exception.Message);
    }

    // Extra bytes kept after the block header in a short final block, and the frames they hold:
    // MS mono 2 + 16, MS stereo 2 + 8, IMA mono 1 + 16, IMA stereo 1 + 8 (one full group of 8 frames).
    public static TheoryData<string, int, int, int> ShortBlocks => new()
    {
        { "adpcm-ms-mono-22050", 1, 7, 18 },
        { "adpcm-ms-stereo-22050", 2, 14, 10 },
        { "adpcm-ima-mono-22050", 1, 4, 17 },
        { "adpcm-ima-stereo-22050", 2, 8, 9 },
    };

    [Theory]
    [MemberData(nameof(ShortBlocks))]
    public void Decode_ShortFinalBlock_DecodesTheHeaderAndTheCompleteNibblesPresent(
        string name, int channelCount, int headerSize, int tailFrames)
    {
        var reference = WavDecoder.Decode(AudioFixtures.ReadBytes(name + ".reference.wav"), name + ".reference.wav");
        var bytes = CutLastBlock(AudioFixtures.ReadBytes(name + ".wav"), headerSize + 8, out var fullBlocks, out var samplesPerBlock);
        RenameChunk(bytes, "fact", "junk");

        var decoded = WavDecoder.Decode(bytes, Source);

        var expectedFrames = fullBlocks * samplesPerBlock + tailFrames;
        Assert.Equal(expectedFrames * channelCount, decoded.Samples.Length);
        Assert.Equal(reference.Samples.AsSpan(0, decoded.Samples.Length).ToArray(), decoded.Samples);
    }

    [Theory]
    [MemberData(nameof(ShortBlocks))]
    public void Decode_ShortFinalBlock_IsTrimmedByTheFactChunk(string name, int channelCount, int headerSize, int tailFrames)
    {
        var reference = WavDecoder.Decode(AudioFixtures.ReadBytes(name + ".reference.wav"), name + ".reference.wav");
        var bytes = CutLastBlock(AudioFixtures.ReadBytes(name + ".wav"), headerSize + 8, out var fullBlocks, out var samplesPerBlock);
        var factFrames = fullBlocks * samplesPerBlock + 1;
        BitConverter.GetBytes((uint)factFrames).CopyTo(bytes, IndexOfChunk(bytes, "fact") + 8);

        var decoded = WavDecoder.Decode(bytes, Source);

        Assert.Equal(factFrames * channelCount, decoded.Samples.Length);
        Assert.Equal(reference.Samples.AsSpan(0, decoded.Samples.Length).ToArray(), decoded.Samples);
    }

    [Theory]
    [MemberData(nameof(ShortBlocks))]
    public void Decode_FinalFragmentShorterThanTheBlockHeader_Throws(string name, int channelCount, int headerSize, int tailFrames)
    {
        var bytes = CutLastBlock(AudioFixtures.ReadBytes(name + ".wav"), headerSize - 1, out _, out _);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("truncated", exception.Message);
        Assert.Contains("block header", exception.Message);
    }

    [Theory]
    [MemberData(nameof(ShortBlocks))]
    public void Decode_ShortFinalBlockHoldingOnlyTheHeader_GivesTheHeaderFrames(string name, int channelCount, int headerSize, int tailFrames)
    {
        var bytes = CutLastBlock(AudioFixtures.ReadBytes(name + ".wav"), headerSize, out var fullBlocks, out var samplesPerBlock);
        RenameChunk(bytes, "fact", "junk");

        var decoded = WavDecoder.Decode(bytes, Source);

        var headerFrames = name.Contains("ms") ? 2 : 1;
        Assert.Equal((fullBlocks * samplesPerBlock + headerFrames) * channelCount, decoded.Samples.Length);
    }

    /// <summary>
    /// Cuts the last block of an ffmpeg fixture down to <paramref name="keptBytes"/> bytes and patches the 'data'
    /// (and RIFF) sizes. The data chunk is the last chunk of the fixtures.
    /// </summary>
    private static byte[] CutLastBlock(byte[] wav, int keptBytes, out int fullBlocks, out int samplesPerBlock)
    {
        int blockAlign = BitConverter.ToUInt16(wav, 32);
        samplesPerBlock = BitConverter.ToUInt16(wav, 38);
        var dataChunk = IndexOfChunk(wav, "data");
        var dataSize = (int)BitConverter.ToUInt32(wav, dataChunk + 4);
        Assert.Equal(0, dataSize % blockAlign);
        fullBlocks = dataSize / blockAlign - 1;

        var newDataSize = fullBlocks * blockAlign + keptBytes;
        var cut = wav.AsSpan(0, dataChunk + 8 + newDataSize).ToArray();
        BitConverter.GetBytes((uint)newDataSize).CopyTo(cut, dataChunk + 4);
        BitConverter.GetBytes((uint)(cut.Length - 8)).CopyTo(cut, 4);
        return cut;
    }

    [Fact]
    public void Decode_MsAdpcmWithoutACoefficientTable_Throws()
    {
        var bytes = AudioFixtures.ReadBytes("adpcm-ms-mono-22050.wav");
        // wNumCoef is the 16 bit field at offset 20 of the 'fmt ' body (the body starts at 20).
        BitConverter.GetBytes((ushort)0).CopyTo(bytes, 40);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("coefficient", exception.Message);
    }

    [Fact]
    public void Decode_MsAdpcmWithATableLargerThanTheFormatChunk_Throws()
    {
        var bytes = AudioFixtures.ReadBytes("adpcm-ms-mono-22050.wav");
        BitConverter.GetBytes((ushort)200).CopyTo(bytes, 40);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("coefficient", exception.Message);
    }

    [Fact]
    public void Decode_MsAdpcmWithoutTheFormatExtension_Throws()
    {
        // A plain 16 byte 'fmt ' chunk: no samples per block, no coefficients.
        var wav = WavBuilder.Create(2, 22050, 1, 4, new byte[16]);
        BitConverter.GetBytes((ushort)256).CopyTo(wav, 32);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("coefficient", exception.Message);
    }

    [Fact]
    public void Decode_MsAdpcmWithAnOutOfRangePredictor_Throws()
    {
        var bytes = AudioFixtures.ReadBytes("adpcm-ms-mono-22050.wav");
        bytes[IndexOfChunk(bytes, "data") + 8] = 200;

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("predictor", exception.Message);
    }

    [Fact]
    public void Decode_ImaAdpcmWithAnOutOfRangeStepIndex_Throws()
    {
        var bytes = AudioFixtures.ReadBytes("adpcm-ima-mono-22050.wav");
        bytes[IndexOfChunk(bytes, "data") + 8 + 2] = 200;

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("step index", exception.Message);
    }

    [Theory]
    [InlineData("adpcm-ms-mono-22050.wav")]
    [InlineData("adpcm-ima-mono-22050.wav")]
    public void Decode_SamplesPerBlockLargerThanTheBlock_Throws(string name)
    {
        var bytes = AudioFixtures.ReadBytes(name);
        BitConverter.GetBytes((ushort)60000).CopyTo(bytes, 38);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("samples per", exception.Message);
    }

    [Theory]
    [InlineData("adpcm-ms-mono-22050.wav")]
    [InlineData("adpcm-ima-mono-22050.wav")]
    public void Decode_FactLargerThanTheBlocks_Throws(string name)
    {
        var bytes = AudioFixtures.ReadBytes(name);
        BitConverter.GetBytes(1_000_000u).CopyTo(bytes, IndexOfChunk(bytes, "fact") + 8);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("fact", exception.Message);
    }

    [Fact]
    public void Decode_AdpcmWithAnotherBitDepth_Throws()
    {
        var bytes = AudioFixtures.ReadBytes("adpcm-ima-mono-22050.wav");
        BitConverter.GetBytes((ushort)8).CopyTo(bytes, 34);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(bytes, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_AdpcmInsideExtensible_IsNotSupported()
    {
        var wav = WavBuilder.CreateExtensible(0x11, 22050, 1, 16, new byte[8]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("EXTENSIBLE", exception.Message);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void AudioClipLoader_LoadsAnAdpcmWavAsAPcmClip(string name, int channelCount)
    {
        var asset = new AudioClipLoader().LoadAsset(AudioFixtures.GetPath(name + ".wav"), null);

        var clip = Assert.IsType<PcmAudioClip>(asset);
        Assert.Equal(22050, clip.SampleRate);
        Assert.Equal(channelCount, clip.ChannelCount);
        Assert.Equal(FactFrames, clip.FrameCount);
    }

    private static int IndexOfChunk(byte[] wav, string id)
    {
        var index = wav.AsSpan(12).IndexOf(Encoding.ASCII.GetBytes(id));
        Assert.True(index >= 0, $"chunk '{id}' not found");
        return index + 12;
    }

    private static void RenameChunk(byte[] wav, string id, string newId)
    {
        Encoding.ASCII.GetBytes(newId).CopyTo(wav, IndexOfChunk(wav, id));
    }
}
