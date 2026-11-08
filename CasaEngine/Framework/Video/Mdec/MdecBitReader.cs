using System.Buffers.Binary;

namespace CasaEngine.Framework.Video.Mdec;

/// <summary>
/// Bit reader of the BS bitstream (docs/engine/video.md section 1.3): the stream is a sequence of little-endian
/// 16-bit halfwords and the bits of each halfword are consumed from bit 15 down to bit 0. Peeking beyond the end of the
/// data reads zero bits; consuming beyond the end throws an exception that names the frame. A trailing odd byte is
/// ignored. No allocation.
/// </summary>
internal ref struct MdecBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _frameIndex;
    private int _position;
    private ulong _buffer;
    private int _bufferedBits;
    private long _bitsLeft;

    /// <param name="data">The bitstream, starting at byte 8 of the frame.</param>
    /// <param name="frameIndex">Frame number used in error messages.</param>
    public MdecBitReader(ReadOnlySpan<byte> data, int frameIndex)
    {
        _data = data;
        _frameIndex = frameIndex;
        _position = 0;
        _buffer = 0;
        _bufferedBits = 0;
        _bitsLeft = (long)(data.Length & ~1) * 8;
    }

    public readonly long BitsLeft => _bitsLeft;

    /// <summary>Returns the next <paramref name="count"/> bits (1 to 32) without consuming them.</summary>
    public uint Peek(int count)
    {
        Fill();
        return (uint)(_buffer >> (64 - count));
    }

    /// <summary>Consumes <paramref name="count"/> bits; throws when fewer remain in the frame.</summary>
    public void Skip(int count)
    {
        if (count > _bitsLeft)
        {
            throw new InvalidDataException(
                $"MDEC frame {_frameIndex}: the bitstream is truncated (a code needs {count} bits, {_bitsLeft} remain).");
        }

        _buffer <<= count;
        _bufferedBits -= count;
        _bitsLeft -= count;
    }

    /// <summary>Reads and consumes <paramref name="count"/> bits (1 to 32).</summary>
    public uint Read(int count)
    {
        var value = Peek(count);
        Skip(count);
        return value;
    }

    private void Fill()
    {
        while (_bufferedBits <= 48)
        {
            ulong halfword = 0;
            if (_position + 2 <= _data.Length)
            {
                halfword = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_position, 2));
                _position += 2;
            }

            // Past the end the buffer is padded with zero bits; _bitsLeft tells how many are real.
            _buffer |= halfword << (48 - _bufferedBits);
            _bufferedBits += 16;
        }
    }
}
