using CasaEngine.Framework.Audio.Decoding;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class WavDecoderTests
{
    private const string Source = "unit-test.wav";

    [Fact]
    public void Decode_Pcm8_RecentersAndScales()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 11025, 1, 8, new byte[] { 0, 128, 255, 129 });

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { -32768, 0, 32512, 256 }, decoded.Samples);
        Assert.Equal(11025, decoded.SampleRate);
        Assert.Equal(1, decoded.ChannelCount);
    }

    [Fact]
    public void Decode_Pcm16_Stereo_KeepsInterleavedSamples()
    {
        var data = Int16Bytes(100, -100, short.MaxValue, short.MinValue);
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 2, 16, data);

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { 100, -100, short.MaxValue, short.MinValue }, decoded.Samples);
        Assert.Equal(22050, decoded.SampleRate);
        Assert.Equal(2, decoded.ChannelCount);
    }

    [Fact]
    public void Decode_Pcm24_KeepsTheTop16Bits()
    {
        // 0x123456, -1 (0xFFFFFF), 0x800000 (most negative), 0x7FFFFF (most positive).
        var data = new byte[] { 0x56, 0x34, 0x12, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x80, 0xFF, 0xFF, 0x7F };
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 44100, 1, 24, data);

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { 0x1234, -1, short.MinValue, short.MaxValue }, decoded.Samples);
    }

    [Fact]
    public void Decode_Pcm32_Stereo_ShiftsRight16()
    {
        var data = Int32Bytes(0x12345678, -1, int.MinValue, int.MaxValue);
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 48000, 2, 32, data);

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { 0x1234, -1, short.MinValue, short.MaxValue }, decoded.Samples);
        Assert.Equal(2, decoded.ChannelCount);
    }

    [Fact]
    public void Decode_Float32_ClampsScalesAndRounds()
    {
        var data = FloatBytes(0f, 1f, -1f, 0.5f, 2f, -3f, float.NaN);
        var wav = WavBuilder.Create(WavBuilder.IeeeFloatFormatTag, 44100, 1, 32, data);

        var decoded = WavDecoder.Decode(wav, Source);

        // 0.5 * 32767 = 16383.5 rounds to even (16384); out of range values clamp; NaN is silence.
        Assert.Equal(new short[] { 0, 32767, -32767, 16384, 32767, -32767, 0 }, decoded.Samples);
    }

    [Fact]
    public void Decode_Float32_Stereo()
    {
        var wav = WavBuilder.Create(WavBuilder.IeeeFloatFormatTag, 32000, 2, 32, FloatBytes(1f, -1f));

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { 32767, -32767 }, decoded.Samples);
        Assert.Equal(2, decoded.ChannelCount);
    }

    [Fact]
    public void Decode_ExtensiblePcm16_Stereo()
    {
        var wav = WavBuilder.CreateExtensible(WavBuilder.PcmFormatTag, 22050, 2, 16, Int16Bytes(1, 2, 3, 4));

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { 1, 2, 3, 4 }, decoded.Samples);
    }

    [Fact]
    public void Decode_ExtensiblePcm24_Mono()
    {
        var wav = WavBuilder.CreateExtensible(WavBuilder.PcmFormatTag, 22050, 1, 24, new byte[] { 0x56, 0x34, 0x12 });

        Assert.Equal(new short[] { 0x1234 }, WavDecoder.Decode(wav, Source).Samples);
    }

    [Fact]
    public void Decode_ExtensibleFloat_Mono()
    {
        var wav = WavBuilder.CreateExtensible(WavBuilder.IeeeFloatFormatTag, 22050, 1, 32, FloatBytes(-1f, 1f));

        Assert.Equal(new short[] { -32767, 32767 }, WavDecoder.Decode(wav, Source).Samples);
    }

    [Fact]
    public void Decode_SkipsAnUnknownOddSizedChunkBeforeData()
    {
        // 5 bytes + 1 padding byte: a wrong padding would shift the data chunk and break the parse.
        var wav = WavBuilder.Create(
            WavBuilder.PcmFormatTag, 22050, 1, 16, Int16Bytes(7, -7), extraChunk: new byte[] { 1, 2, 3, 4, 5 }, extraChunkId: "LIST");

        var decoded = WavDecoder.Decode(wav, Source);

        Assert.Equal(new short[] { 7, -7 }, decoded.Samples);
    }

    [Fact]
    public void Decode_AcceptsAnEighteenByteFormatChunk()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 16, Int16Bytes(5), formatChunkSize: 18);

        Assert.Equal(new short[] { 5 }, WavDecoder.Decode(wav, Source).Samples);
    }

    [Fact]
    public void Decode_EmptyDataChunk_GivesNoSamples()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 2, 16, Array.Empty<byte>());

        Assert.Empty(WavDecoder.Decode(wav, Source).Samples);
    }

    [Fact]
    public void Decode_NotRiff_Throws()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => WavDecoder.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_TooShort_Throws()
    {
        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(new byte[] { 0x52, 0x49 }, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_MissingDataChunk_Throws()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 16, Array.Empty<byte>());
        // Rename the data chunk (it is the last 8 bytes) so no data chunk remains.
        wav[36] = (byte)'j';
        wav[37] = (byte)'u';
        wav[38] = (byte)'n';
        wav[39] = (byte)'k';

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("data", exception.Message);
    }

    [Fact]
    public void Decode_MissingFormatChunk_Throws()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 16, Int16Bytes(1, 2));
        wav[12] = (byte)'j';
        wav[13] = (byte)'u';
        wav[14] = (byte)'n';
        wav[15] = (byte)'k';

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_DataBeforeFormat_Throws()
    {
        var wav = new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 12, 0, 0, 0, (byte)'W', (byte)'A', (byte)'V', (byte)'E',
            (byte)'d', (byte)'a', (byte)'t', (byte)'a', 0, 0, 0, 0,
        };

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_DataLargerThanTheFile_Throws()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 16, Int16Bytes(1, 2, 3, 4));
        // Declare 1000 data bytes while only 8 are present (the size field precedes the data).
        BitConverter.GetBytes(1000).CopyTo(wav, wav.Length - 8 - 4);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("truncated", exception.Message);
    }

    [Fact]
    public void Decode_DataNotAWholeNumberOfBlocks_Throws()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 2, 16, new byte[6]);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_InconsistentBlockAlign_Throws()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 2, 16, new byte[8]);
        BitConverter.GetBytes((ushort)3).CopyTo(wav, 32);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("block align", exception.Message);
    }

    [Fact]
    public void Decode_ZeroSampleRate_Throws()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 0, 1, 16, new byte[4]);

        var exception = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Theory]
    [InlineData(2)] // MS-ADPCM
    [InlineData(0x11)] // IMA ADPCM
    [InlineData(6)] // A-law
    public void Decode_OtherFormatTag_IsNotSupported(int formatTag)
    {
        var wav = WavBuilder.Create(formatTag, 22050, 1, 4, new byte[16]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains($"format tag {formatTag}", exception.Message);
    }

    [Fact]
    public void Decode_ExtensibleWithAnotherSubFormat_IsNotSupported()
    {
        var wav = WavBuilder.CreateExtensible(2, 22050, 1, 16, new byte[8]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_MoreThanTwoChannels_IsNotSupported()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 6, 16, new byte[24]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
        Assert.Contains("6 channels", exception.Message);
    }

    [Fact]
    public void Decode_ZeroChannels_IsNotSupported()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 0, 16, new byte[4]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_UnsupportedBitDepth_IsNotSupported()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 12, new byte[8]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    [Fact]
    public void Decode_Float64_IsNotSupported()
    {
        var wav = WavBuilder.Create(WavBuilder.IeeeFloatFormatTag, 22050, 1, 64, new byte[16]);

        var exception = Assert.Throws<NotSupportedException>(() => WavDecoder.Decode(wav, Source));

        Assert.Contains(Source, exception.Message);
    }

    private static byte[] Int16Bytes(params short[] values)
    {
        var bytes = new byte[values.Length * sizeof(short)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] Int32Bytes(params int[] values)
    {
        var bytes = new byte[values.Length * sizeof(int)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static byte[] FloatBytes(params float[] values)
    {
        var bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
