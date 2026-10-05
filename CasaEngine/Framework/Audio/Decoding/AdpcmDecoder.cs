using System.Buffers.Binary;

namespace CasaEngine.Framework.Audio.Decoding;

/// <summary>
/// Block decoders for the two ADPCM encodings found in wav files: Microsoft ADPCM (format tag 2) and
/// IMA/DVI ADPCM (format tag 0x11). Both produce interleaved 16 bit PCM. Used for resident clips only.
/// </summary>
/// <remarks>
/// Sources (documentation only, no third party code; each link was fetched and checked):
/// - https://wiki.multimedia.cx/index.php/Microsoft_ADPCM : Microsoft ADPCM block header (predictor, delta,
///   two samples per channel), high nibble first, the 16 value adaptation table, the seven standard
///   coefficient pairs (here read from the 'fmt ' chunk), the decoding formula and the delta floor of 16.
/// - https://wiki.multimedia.cx/index.php/Microsoft_IMA_ADPCM : IMA ADPCM wav block layout: per-channel header
///   (little endian predictor, step index, one unused byte), then 4 byte groups per channel interleaved,
///   low nibble first.
/// - https://wiki.multimedia.cx/index.php/IMA_ADPCM : the 89 entry step table and the index table.
/// - https://www.cs.columbia.edu/~hgs/audio/dvi/IMA_ADPCM.pdf : Interactive Multimedia Association,
///   "Recommended Practices for Enhancing Digital Audio Compatibility in Multimedia Systems", revision 3.00,
///   21 October 1992, section 6.2 (4 bit ADPCM decompression) and its step and index tables.
/// The Microsoft Learn pages for ADPCMWAVEFORMAT / IMAADPCMWAVEFORMAT could not be fetched (HTTP 404) and are
/// not cited; the 'fmt ' extension layout (cbSize, samples per block, coefficient count and pairs) follows the
/// first wiki page above and is validated against the chunk size.
/// </remarks>
internal static class AdpcmDecoder
{
    // Microsoft ADPCM: next delta = delta * table[nibble] / 256.
    private static readonly int[] MsAdaptationTable =
    {
        230, 230, 230, 230, 307, 409, 512, 614, 768, 614, 512, 409, 307, 230, 230, 230,
    };

    private const int MsMinDelta = 16;

    // IMA ADPCM: index adjustment by the low 3 bits of the nibble, and the 89 quantizer step sizes.
    private static readonly int[] ImaIndexTable = { -1, -1, -1, -1, 2, 4, 6, 8 };

    private static readonly int[] ImaStepTable =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97,
        107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
        876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428,
        4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350,
        22385, 24623, 27086, 29794, 32767,
    };

    /// <summary>
    /// Decodes whole Microsoft ADPCM blocks.
    /// </summary>
    /// <param name="data">The 'data' chunk: whole blocks, the last one possibly short.</param>
    /// <param name="coefficients">Pairs (coef1, coef2) read from the 'fmt ' chunk, two entries per predictor.</param>
    public static short[] DecodeMs(
        ReadOnlySpan<byte> data, int channelCount, int blockAlign, int samplesPerBlock, ReadOnlySpan<short> coefficients, string sourceName)
    {
        var headerSize = 7 * channelCount;
        var coefficientSetCount = coefficients.Length / 2;

        if (blockAlign < headerSize)
        {
            throw new InvalidDataException(
                $"'{sourceName}' has a Microsoft ADPCM block align of {blockAlign}, smaller than the {headerSize} byte block header.");
        }

        // The block header holds frames 0 and 1; every other frame is one nibble per channel.
        var capacity = (blockAlign - headerSize) * 2 / channelCount + 2;
        if (samplesPerBlock < 2 || samplesPerBlock > capacity)
        {
            throw new InvalidDataException(
                $"'{sourceName}' declares {samplesPerBlock} samples per Microsoft ADPCM block but a {blockAlign} byte block holds at most {capacity}.");
        }

        var blockCount = CountBlocks(data.Length, blockAlign, headerSize, sourceName, "Microsoft ADPCM");
        var tailLength = data.Length % blockAlign;
        var tailFrames = tailLength == 0 ? 0 : MsFramesIn(tailLength, channelCount, samplesPerBlock);
        var output = new short[((data.Length / blockAlign) * samplesPerBlock + tailFrames) * channelCount];
        Span<int> delta = stackalloc int[2];
        Span<int> sample1 = stackalloc int[2];
        Span<int> sample2 = stackalloc int[2];
        Span<int> coef1 = stackalloc int[2];
        Span<int> coef2 = stackalloc int[2];

        for (var block = 0; block < blockCount; block++)
        {
            var source = data.Slice(block * blockAlign, Math.Min(blockAlign, data.Length - block * blockAlign));
            var frameCount = source.Length == blockAlign ? samplesPerBlock : tailFrames;
            var destination = output.AsSpan(block * samplesPerBlock * channelCount, frameCount * channelCount);

            // Header: one predictor byte per channel, then delta, sample 1 and sample 2 as int16 per channel.
            for (var ch = 0; ch < channelCount; ch++)
            {
                int predictor = source[ch];
                if (predictor >= coefficientSetCount)
                {
                    throw new InvalidDataException(
                        $"'{sourceName}' has a Microsoft ADPCM block {block} whose predictor index {predictor} is outside its {coefficientSetCount} coefficient sets.");
                }

                coef1[ch] = coefficients[predictor * 2];
                coef2[ch] = coefficients[predictor * 2 + 1];
                delta[ch] = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(channelCount + 2 * ch, 2));
                sample1[ch] = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(3 * channelCount + 2 * ch, 2));
                sample2[ch] = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(5 * channelCount + 2 * ch, 2));

                // The oldest sample comes first in the output.
                destination[ch] = (short)sample2[ch];
                destination[channelCount + ch] = (short)sample1[ch];
            }

            // Nibbles are interleaved by frame; the high nibble of a byte comes first (left channel in stereo).
            var nibbleIndex = 0;
            for (var frame = 2; frame < frameCount; frame++)
            {
                for (var ch = 0; ch < channelCount; ch++, nibbleIndex++)
                {
                    var packed = source[headerSize + (nibbleIndex >> 1)];
                    var nibble = (nibbleIndex & 1) == 0 ? packed >> 4 : packed & 0x0F;
                    var signed = nibble >= 8 ? nibble - 16 : nibble;

                    var predicted = (sample1[ch] * coef1[ch] + sample2[ch] * coef2[ch]) / 256 + signed * delta[ch];
                    predicted = Math.Clamp(predicted, short.MinValue, short.MaxValue);

                    sample2[ch] = sample1[ch];
                    sample1[ch] = predicted;
                    destination[frame * channelCount + ch] = (short)predicted;

                    delta[ch] = Math.Max(MsMinDelta, MsAdaptationTable[nibble] * delta[ch] / 256);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Decodes whole IMA/DVI ADPCM blocks (the wav variant: 4 bit samples, 4 byte groups per channel).
    /// </summary>
    /// <param name="data">The 'data' chunk: whole blocks, the last one possibly short.</param>
    public static short[] DecodeIma(
        ReadOnlySpan<byte> data, int channelCount, int blockAlign, int samplesPerBlock, string sourceName)
    {
        var headerSize = 4 * channelCount;
        var groupSize = 4 * channelCount;

        if (blockAlign < headerSize || (blockAlign - headerSize) % groupSize != 0)
        {
            throw new InvalidDataException(
                $"'{sourceName}' has an IMA ADPCM block align of {blockAlign}, which is not a {headerSize} byte header plus whole {groupSize} byte groups.");
        }

        // The header holds frame 0; every group then holds 8 frames per channel.
        var capacity = (blockAlign - headerSize) / groupSize * 8 + 1;
        if (samplesPerBlock < 1 || samplesPerBlock > capacity)
        {
            throw new InvalidDataException(
                $"'{sourceName}' declares {samplesPerBlock} samples per IMA ADPCM block but a {blockAlign} byte block holds at most {capacity}.");
        }

        var blockCount = CountBlocks(data.Length, blockAlign, headerSize, sourceName, "IMA ADPCM");
        var tailLength = data.Length % blockAlign;
        var tailFrames = tailLength == 0 ? 0 : ImaFramesIn(tailLength, channelCount, samplesPerBlock);
        var output = new short[((data.Length / blockAlign) * samplesPerBlock + tailFrames) * channelCount];
        Span<int> predictor = stackalloc int[2];
        Span<int> index = stackalloc int[2];

        for (var block = 0; block < blockCount; block++)
        {
            var source = data.Slice(block * blockAlign, Math.Min(blockAlign, data.Length - block * blockAlign));
            var frameCount = source.Length == blockAlign ? samplesPerBlock : tailFrames;
            var destination = output.AsSpan(block * samplesPerBlock * channelCount, frameCount * channelCount);

            // Header per channel: int16 sample, step index byte, one reserved byte.
            for (var ch = 0; ch < channelCount; ch++)
            {
                predictor[ch] = BinaryPrimitives.ReadInt16LittleEndian(source.Slice(4 * ch, 2));
                index[ch] = source[4 * ch + 2];
                if (index[ch] >= ImaStepTable.Length)
                {
                    throw new InvalidDataException(
                        $"'{sourceName}' has an IMA ADPCM block {block} whose step index {index[ch]} is outside 0..{ImaStepTable.Length - 1}.");
                }

                destination[ch] = (short)predictor[ch];
            }

            // Then groups of 4 bytes (8 nibbles, low nibble first) per channel, channels interleaved by group.
            for (var frame = 1; frame < frameCount; frame++)
            {
                var offset = frame - 1;
                var group = offset >> 3;
                var inGroup = offset & 7;

                for (var ch = 0; ch < channelCount; ch++)
                {
                    var packed = source[headerSize + group * groupSize + ch * 4 + (inGroup >> 1)];
                    var nibble = (inGroup & 1) == 0 ? packed & 0x0F : packed >> 4;

                    var step = ImaStepTable[index[ch]];
                    // Reconstruction (magnitude + 1/2) * step / 4 with a single truncation. The IMA reference
                    // (section 6.2 of the PDF above) adds step>>3, step>>2, step>>1 and step for the set bits,
                    // truncating each term: it differs from the closed form by up to 2 per sample. The closed
                    // form is deliberate: it is what ffmpeg (which encoded and decodes the test fixtures)
                    // computes, and the fixtures match its output sample for sample.
                    var difference = ((2 * (nibble & 7) + 1) * step) >> 3;

                    predictor[ch] = Math.Clamp(
                        (nibble & 8) != 0 ? predictor[ch] - difference : predictor[ch] + difference,
                        short.MinValue,
                        short.MaxValue);
                    index[ch] = Math.Clamp(index[ch] + ImaIndexTable[nibble & 7], 0, ImaStepTable.Length - 1);
                    destination[frame * channelCount + ch] = (short)predictor[ch];
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Number of blocks, the last one possibly short. A short final block is decoded when it holds at least the
    /// per-channel block header; a fragment smaller than the header is a truncation.
    /// </summary>
    private static int CountBlocks(int dataLength, int blockAlign, int headerSize, string sourceName, string encoding)
    {
        var tailLength = dataLength % blockAlign;
        if (tailLength != 0 && tailLength < headerSize)
        {
            throw new InvalidDataException(
                $"'{sourceName}' has a truncated {encoding} block: the last {tailLength} bytes of its 'data' chunk are shorter than the {headerSize} byte block header.");
        }

        return dataLength / blockAlign + (tailLength != 0 ? 1 : 0);
    }

    // Frames held by a short Microsoft ADPCM block: 2 from the header, then one per complete set of nibbles.
    private static int MsFramesIn(int length, int channelCount, int samplesPerBlock)
    {
        return Math.Min(samplesPerBlock, 2 + (length - 7 * channelCount) * 2 / channelCount);
    }

    // Frames held by a short IMA ADPCM block: 1 from the header, 8 per complete group, then the frames of the
    // partial group whose bytes are present for every channel (the last channel's bytes come last).
    private static int ImaFramesIn(int length, int channelCount, int samplesPerBlock)
    {
        var groupSize = 4 * channelCount;
        var body = length - groupSize;
        var partial = Math.Clamp((body % groupSize - (channelCount - 1) * 4) * 2, 0, 8);
        return Math.Min(samplesPerBlock, 1 + body / groupSize * 8 + partial);
    }
}
