namespace CasaEngine.Tests.Video;

/// <summary>
/// The synthetic clip of Projects/VideoSample (plan choice C8): 160x112 pixels, 60 frames at 15 per second, built with
/// <see cref="MdecTestEncoder"/> and <see cref="MdecTestContainerWriter"/>. Every number is an integer, so the bytes are
/// the same on every machine; a data test compares them with the committed file.
/// </summary>
internal static class VideoSampleClip
{
    public const int Width = 160;
    public const int Height = 112;
    public const int FrameCount = 60;
    public const uint FramesPerSecond = 15;

    private const int QuantScale = 2;
    private const int MacroblockColumns = Width / 16;
    private const int MacroblockRows = Height / 16;

    /// <summary>The whole <c>.mdecvideo</c> file.</summary>
    public static byte[] Build()
    {
        var frames = new List<byte[]>(FrameCount);
        for (var frame = 0; frame < FrameCount; frame++)
        {
            frames.Add(BuildFrame(frame));
        }

        return MdecTestContainerWriter.Write(frames, Width, Height, FramesPerSecond, 1);
    }

    /// <summary>
    /// One version 2 frame. Macroblocks are stored column by column. Luma runs in diagonal bars that move with the frame
    /// number, chroma drifts with the frame number along the rows and the columns, and a few luma blocks carry one AC
    /// coefficient, so the picture changes everywhere at every frame.
    /// </summary>
    private static byte[] BuildFrame(int frame)
    {
        var blocks = new List<MdecTestBlock>(MacroblockColumns * MacroblockRows * 6);

        for (var column = 0; column < MacroblockColumns; column++)
        {
            for (var row = 0; row < MacroblockRows; row++)
            {
                var luma = Triangle(column * 7 + row * 7 + frame * 5, 70, 80);
                var cr = Triangle(frame * 4 + column * 9 + 24, 96, 60);
                var cb = Triangle(frame * 4 + row * 12, 96, 60);
                var hasAc = (column + row + frame) % 3 == 0;

                blocks.Add(new MdecTestBlock(cr * 4));
                blocks.Add(new MdecTestBlock(cb * 4));
                blocks.Add(new MdecTestBlock((luma - 12) * 4));
                blocks.Add(hasAc
                    ? new MdecTestBlock((luma + 12) * 4, MdecTestAc.Of(0, 6))
                    : new MdecTestBlock((luma + 12) * 4));
                blocks.Add(new MdecTestBlock((luma + 12) * 4));
                blocks.Add(hasAc
                    ? new MdecTestBlock((luma - 12) * 4, MdecTestAc.Of(1, -5))
                    : new MdecTestBlock((luma - 12) * 4));
            }
        }

        return MdecTestEncoder.Encode(2, QuantScale, blocks);
    }

    /// <summary>A triangle wave of the given period, from -amplitude to +amplitude, for any integer position.</summary>
    private static int Triangle(int position, int period, int amplitude)
    {
        var p = ((position % period) + period) % period;
        var half = period / 2;
        var v = p < half ? p : period - p;
        return v * 2 * amplitude / half - amplitude;
    }
}
