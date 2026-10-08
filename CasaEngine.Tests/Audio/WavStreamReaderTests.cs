using CasaEngine.Framework.Audio.Streaming;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class WavStreamReaderTests
{
    private static WavStreamReader Open(byte[] wav) => new(new MemoryStream(wav));

    [Theory]
    [InlineData(16)]
    [InlineData(18)]
    [InlineData(40)]
    public void Header_AcceptsTheUsualFormatChunkSizes(int formatChunkSize)
    {
        // The provided music file has an 18 byte 'fmt ' chunk, so 16 is not enough.
        var wav = WavBuilder.CreatePcm16(formatChunkSize: formatChunkSize);

        using var reader = Open(wav);

        Assert.Equal(22050, reader.Format.SampleRate);
        Assert.Equal(2, reader.Format.ChannelCount);
        Assert.Equal(16, reader.Format.BitsPerSample);
        Assert.Equal(4, reader.Format.BlockAlign);
    }

    [Fact]
    public void Header_SkipsUnknownChunksBetweenFmtAndData()
    {
        var wav = WavBuilder.CreatePcm16(extraChunk: new byte[] { 1, 2, 3, 4, 5, 6 }, extraChunkId: "LIST");

        using var reader = Open(wav);

        Assert.Equal(400, reader.Format.DataLength);
    }

    [Fact]
    public void Header_SkipsAnOddSizedChunkAndItsPaddingByte()
    {
        var wav = WavBuilder.CreatePcm16(extraChunk: new byte[] { 1, 2, 3 }, extraChunkId: "fact");

        using var reader = Open(wav);

        Assert.Equal(400, reader.Format.DataLength);
    }

    [Fact]
    public void Format_ExposesTheDuration()
    {
        var wav = WavBuilder.CreatePcm16(sampleRate: 100, channelCount: 2, sampleCount: 200);

        using var reader = Open(wav);

        Assert.Equal(2.0, reader.Format.Duration.TotalSeconds, 3);
    }

    [Fact]
    public void Read_ReturnsTheAudioBytesInOrder()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 10);
        var expected = WavBuilder.ExpectedData(wav, 40);
        using var reader = Open(wav);
        var buffer = new byte[16];
        var actual = new List<byte>();

        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            actual.AddRange(buffer[..read]);
        }

        Assert.Equal(expected, actual.ToArray());
        Assert.True(reader.IsEndOfStream);
    }

    [Fact]
    public void Read_NeverSplitsABlock()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 10);
        using var reader = Open(wav);
        var buffer = new byte[7];

        var read = reader.Read(buffer, 0, buffer.Length);

        // 7 bytes asked for, block align is 4: only one whole block comes back.
        Assert.Equal(4, read);
    }

    [Fact]
    public void Read_ReturnsZeroWhenTheRequestIsSmallerThanABlock()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 10);
        using var reader = Open(wav);

        Assert.Equal(0, reader.Read(new byte[4], 0, 3));
    }

    [Fact]
    public void Read_HonoursTheOffset()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 4);
        var expected = WavBuilder.ExpectedData(wav, 16);
        using var reader = Open(wav);
        var buffer = new byte[20];

        var read = reader.Read(buffer, 4, 16);

        Assert.Equal(16, read);
        Assert.Equal(expected, buffer[4..20]);
    }

    [Fact]
    public void Rewind_ReplaysExactlyTheSameBytes()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 8);
        using var reader = Open(wav);
        var first = new byte[32];
        var second = new byte[32];

        reader.Read(first, 0, first.Length);
        Assert.True(reader.IsEndOfStream);

        reader.Rewind();
        Assert.False(reader.IsEndOfStream);
        Assert.Equal(0, reader.Position);

        reader.Read(second, 0, second.Length);

        Assert.Equal(first, second);
    }

    // ---- SeekToSample (plan E19.L-a): the loop start of a streamed track is a frame, not a byte ----

    private static byte[] MonoRamp(int frames)
    {
        var data = new byte[frames * 2];
        for (var f = 0; f < frames; f++)
        {
            BitConverter.TryWriteBytes(data.AsSpan(f * 2, 2), (short)f);
        }

        return WavBuilder.Create(WavBuilder.PcmFormatTag, 44100, 1, 16, data, 16);
    }

    [Fact]
    public void SeekToSample_LandsOnTheFrameFromTheDataOffsetOfTheHeader()
    {
        // 'fmt ' of 18 bytes plus a 6 byte LIST chunk: the data starts at offset 60, not 44.
        using var reader = Open(MusicPlayerLoopStartTests.Ramp(1000, 18, 6));
        Assert.Equal(60, reader.Format.DataOffset);

        reader.SeekToSample(600);

        Assert.Equal(2400, reader.Position);
        var buffer = new byte[16];
        Assert.Equal(16, reader.Read(buffer, 0, 16));
        for (var k = 0; k < 4; k++)
        {
            Assert.Equal(600 + k, BitConverter.ToInt16(buffer, k * 4));
            Assert.Equal(10600 + k, BitConverter.ToInt16(buffer, (k * 4) + 2));
        }

        Assert.Equal(2416, reader.Position);
    }

    [Fact]
    public void SeekToSample_TheLastFrameIsReadableThenTheStreamEnds()
    {
        using var reader = Open(MusicPlayerLoopStartTests.Ramp(1000, 18));
        var buffer = new byte[64];

        reader.SeekToSample(999);

        Assert.Equal(4, reader.Read(buffer, 0, 64));
        Assert.Equal(0, reader.Read(buffer, 0, 64));
        Assert.True(reader.IsEndOfStream);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(1000L)]
    public void SeekToSample_OutsideTheFramesThrows(long frame)
    {
        using var reader = Open(MusicPlayerLoopStartTests.Ramp(1000, 18));

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.SeekToSample(frame));
    }

    [Fact]
    public void SeekToSample_OnADisposedReaderThrows()
    {
        var reader = Open(MusicPlayerLoopStartTests.Ramp(1000, 18));
        reader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.SeekToSample(0));
    }

    [Fact]
    public void SeekToSample_ZeroAndRewind_ReadTheFirstFrameOfAHeaderThatIsNot44Bytes()
    {
        // The data starts at offset 46; frame 0 is (0, 10000). A seek that assumed 44 would read the two header bytes before it.
        using var reader = Open(MusicPlayerLoopStartTests.Ramp(1000, 18));
        Assert.Equal(46, reader.Format.DataOffset);
        var scratch = new byte[64];
        var frame = new byte[4];

        reader.Read(scratch, 0, 64);
        reader.SeekToSample(0);
        Assert.Equal(0, reader.Position);
        Assert.Equal(4, reader.Read(frame, 0, 4));
        Assert.Equal((0, 10000), ((int)BitConverter.ToInt16(frame, 0), (int)BitConverter.ToInt16(frame, 2)));

        reader.Read(scratch, 0, 64);
        reader.Rewind();
        Assert.Equal(4, reader.Read(frame, 0, 4));
        Assert.Equal((0, 10000), ((int)BitConverter.ToInt16(frame, 0), (int)BitConverter.ToInt16(frame, 2)));
    }

    [Fact]
    public void SeekToSample_CountsTheBlockAlignOfAMonoFile()
    {
        using var reader = Open(MonoRamp(1000));

        reader.SeekToSample(600);

        Assert.Equal(1200, reader.Position);
        var buffer = new byte[2];
        reader.Read(buffer, 0, 2);
        Assert.Equal(600, BitConverter.ToInt16(buffer, 0));
    }

    [Fact]
    public void SeekToSample_AllocatesNothing()
    {
        using var reader = Open(MusicPlayerLoopStartTests.Ramp(1000, 18, 6));
        reader.SeekToSample(1);

        var before = AllocationWindow.Start();
        for (var i = 0; i < 1000; i++)
        {
            reader.SeekToSample(i % 1000);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void SeekToSample_OnAHeaderOf18BytesDoesNotLandTwoBytesEarly()
    {
        // 'fmt ' of 18 bytes: the data starts at 46. The naive 44 + 600 * 4 reads (10599, 600), the channels shifted by one sample.
        var wav = MusicPlayerLoopStartTests.Ramp(1000, 18);
        using var reader = Open(wav);
        Assert.Equal(46, reader.Format.DataOffset);

        reader.SeekToSample(600);

        var frame = new byte[4];
        reader.Read(frame, 0, 4);
        Assert.Equal((600, 10600), ((int)BitConverter.ToInt16(frame, 0), (int)BitConverter.ToInt16(frame, 2)));
        Assert.Equal((10599, 600), ((int)BitConverter.ToInt16(wav, 44 + 2400), (int)BitConverter.ToInt16(wav, 44 + 2402)));
    }

    [Fact]
    public void Position_TracksTheBytesRead()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 8);
        using var reader = Open(wav);

        reader.Read(new byte[12], 0, 12);

        Assert.Equal(12, reader.Position);
        Assert.False(reader.IsEndOfStream);
    }

    [Fact]
    public void Read_DoesNotAllocate()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 4096);
        using var reader = Open(wav);
        var buffer = new byte[512];

        reader.Read(buffer, 0, buffer.Length);
        var before = AllocationWindow.Start();

        for (var i = 0; i < 100; i++)
        {
            if (reader.Read(buffer, 0, buffer.Length) == 0)
            {
                reader.Rewind();
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Rejects_AnEightBitWav()
    {
        var wav = WavBuilder.Create(WavBuilder.PcmFormatTag, 22050, 1, 8, new byte[64]);

        var exception = Assert.Throws<NotSupportedException>(() => Open(wav));
        Assert.Contains("8 bit", exception.Message);
    }

    [Fact]
    public void Rejects_AnIeeeFloatWav()
    {
        var wav = WavBuilder.Create(WavBuilder.IeeeFloatFormatTag, 22050, 2, 32, new byte[64]);

        var exception = Assert.Throws<NotSupportedException>(() => Open(wav));
        Assert.Contains("PCM", exception.Message);
    }

    [Fact]
    public void Rejects_AnExtensibleWav()
    {
        var wav = WavBuilder.Create(WavBuilder.ExtensibleFormatTag, 22050, 2, 16, new byte[64], formatChunkSize: 40);

        var exception = Assert.Throws<NotSupportedException>(() => Open(wav));
        Assert.Contains("EXTENSIBLE", exception.Message);
    }

    [Fact]
    public void Rejects_AFileThatIsNotRiff()
    {
        var notAWav = new byte[64];
        notAWav[0] = (byte)'N';

        Assert.Throws<InvalidDataException>(() => Open(notAWav));
    }

    [Fact]
    public void Rejects_AWavWithoutDataChunk()
    {
        var wav = WavBuilder.CreatePcm16(sampleCount: 4);
        // Break the 'data' chunk id, keeping a valid RIFF/WAVE header.
        var dataIndex = IndexOfAscii(wav, "data");
        wav[dataIndex] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => Open(wav));
    }

    [Fact]
    public void Rejects_ANonSeekableStream()
    {
        using var forwardOnly = new ForwardOnlyStream(WavBuilder.CreatePcm16());

        Assert.Throws<ArgumentException>(() => new WavStreamReader(forwardOnly));
    }

    [Fact]
    public void Dispose_LeavesABorrowedStreamOpen()
    {
        var stream = new MemoryStream(WavBuilder.CreatePcm16());

        using (var reader = new WavStreamReader(stream, ownsStream: false))
        {
            Assert.Equal(22050, reader.Format.SampleRate);
        }

        Assert.True(stream.CanRead);
        stream.Dispose();
    }

    private static int IndexOfAscii(byte[] bytes, string value)
    {
        for (var i = 0; i <= bytes.Length - value.Length; i++)
        {
            var found = true;
            for (var j = 0; j < value.Length; j++)
            {
                if (bytes[i + j] != (byte)value[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public ForwardOnlyStream(byte[] content) => _inner = new MemoryStream(content);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
