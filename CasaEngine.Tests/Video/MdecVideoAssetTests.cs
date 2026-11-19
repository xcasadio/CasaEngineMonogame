using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Video;
using Xunit;
using static CasaEngine.Tests.Video.MdecTestContainerWriter;

namespace CasaEngine.Tests.Video;

public class MdecVideoAssetTests
{
    private const string Name = "clips/intro.mdecvideo";

    private static byte[] Frame(int version, int dc)
        => MdecTestEncoder.Encode(version, 4, new[]
        {
            new MdecTestBlock(dc), new MdecTestBlock(0), new MdecTestBlock(0),
            new MdecTestBlock(0), new MdecTestBlock(0), new MdecTestBlock(0),
        });

    private static byte[] ThreeFrames(byte[]? audio = null, uint num = 30, uint den = 1)
        => Write(new[] { Frame(1, 8), Frame(2, 12), Frame(3, 16) }, 20, 14, num, den, audio);

    private static MdecVideoAsset Read(byte[] file) => MdecVideoAsset.Read(new MemoryStream(file), Name);

    private static void AssertError(byte[] file, string field)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read(file));
        Assert.Contains(Name, exception.Message);
        Assert.Contains($"invalid {field}", exception.Message);
    }

    [Fact]
    public void RoundTrip_ReadsTheHeaderAndTheTable()
    {
        var frames = new[] { Frame(1, 8), Frame(2, 12), Frame(3, 16) };
        var file = Write(frames, 20, 14, 30000, 1001);

        var asset = Read(file);

        Assert.Equal(Name, asset.FileName);
        Assert.Equal(20, asset.Width);
        Assert.Equal(14, asset.Height);
        Assert.Equal(3, asset.FrameCount);
        Assert.Equal(30000u, asset.FrameRateNumerator);
        Assert.Equal(1001u, asset.FrameRateDenominator);
        Assert.False(asset.HasAudio);
        Assert.Equal(0u, asset.AudioOffset);
        Assert.Equal(0u, asset.AudioLength);
        Assert.Equal(frames.Max(f => f.Length), asset.MaxFrameLength);
        for (var i = 0; i < frames.Length; i++)
        {
            Assert.Equal(frames[i].Length, asset.GetFrameLength(i));
            Assert.Equal(frames[i], file.AsSpan((int)asset.GetFrameOffset(i), asset.GetFrameLength(i)).ToArray());
        }

        Assert.Equal((uint)file.Length, asset.GetFrameOffset(3));
    }

    [Fact]
    public void RoundTrip_WithAudio_ReadsTheAudioSectionAsBoundsOnly()
    {
        var audio = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
        var file = ThreeFrames(audio);

        var asset = Read(file);

        Assert.True(asset.HasAudio);
        Assert.Equal(7u, asset.AudioLength);
        Assert.Equal(asset.GetFrameOffset(3), asset.AudioOffset);
        Assert.Equal((uint)file.Length, asset.AudioOffset + asset.AudioLength);
    }

    [Fact]
    public void Load_ReadsAFileFromDisk_AndTheLoaderReturnsTheAsset()
    {
        var path = Path.Combine(Path.GetTempPath(), "casa-mdecvideo-" + Guid.NewGuid().ToString("N") + ".mdecvideo");
        File.WriteAllBytes(path, ThreeFrames());
        try
        {
            var loader = new MdecVideoAssetLoader();

            var asset = Assert.IsType<MdecVideoAsset>(loader.LoadAsset(path, new AssetContentManager()));

            Assert.Equal(path, asset.FileName);
            Assert.Equal(3, asset.FrameCount);
            Assert.True(loader.IsFileSupported(path));
            Assert.True(loader.IsFileSupported("A.MDECVIDEO"));
            Assert.False(loader.IsFileSupported("a.sound"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loader_OnAnInvalidFile_ThrowsAnErrorNamingTheFileAndTheField()
    {
        var path = Path.Combine(Path.GetTempPath(), "casa-mdecvideo-" + Guid.NewGuid().ToString("N") + ".mdecvideo");
        var file = ThreeFrames();
        SetU16(file, VersionAt, 9);
        File.WriteAllBytes(path, file);
        try
        {
            var exception = Assert.Throws<InvalidDataException>(
                () => new MdecVideoAssetLoader().LoadAsset(path, new AssetContentManager()));

            Assert.Contains(path, exception.Message);
            Assert.Contains("invalid version", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Extension_IsDeclaredAndTheAssetTypeIsInferredFromIt()
    {
        Assert.Equal(".mdecvideo", Constants.FileNameExtensions.MdecVideo);
        Assert.Equal("mdecvideo", AssetInfo.InferAssetType(@"Videos\intro.mdecvideo"));
    }

    [Fact]
    public void Registry_RegistersTheLoader()
    {
        var manager = new AssetContentManager();
        Assert.False(manager.IsFileSupported("intro.mdecvideo"));

        AssetLoaderRegistry.RegisterLoaders(manager);

        Assert.True(manager.IsFileSupported("intro.mdecvideo"));
    }

    [Fact]
    public void ATruncatedHeader_NamesTheHeader() => AssertError(new byte[10], "header");

    [Fact]
    public void Magic_Invalid()
    {
        var file = ThreeFrames();
        file[0] = (byte)'X';
        AssertError(file, "magic");
    }

    [Fact]
    public void Version_Invalid()
    {
        var file = ThreeFrames();
        SetU16(file, VersionAt, 2);
        AssertError(file, "version");
    }

    [Fact]
    public void Flags_NonZero()
    {
        var file = ThreeFrames();
        SetU16(file, FlagsAt, 1);
        AssertError(file, "flags");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    public void Width_OutOfRange(int width)
    {
        var file = ThreeFrames();
        SetU16(file, WidthAt, width);
        AssertError(file, "width");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    public void Height_OutOfRange(int height)
    {
        var file = ThreeFrames();
        SetU16(file, HeightAt, height);
        AssertError(file, "height");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    public void Dimensions_AtTheLimits_AreAccepted(int size)
    {
        var file = ThreeFrames();
        SetU16(file, WidthAt, size);
        SetU16(file, HeightAt, size);
        var asset = Read(file);
        Assert.Equal(size, asset.Width);
        Assert.Equal(size, asset.Height);
    }

    [Fact]
    public void FrameCount_Zero()
    {
        var file = ThreeFrames();
        SetU32(file, FrameCountAt, 0);
        AssertError(file, "frame count");
    }

    [Fact]
    public void FrameCount_LargerThanTheFile_IsATableError()
    {
        var file = ThreeFrames();
        SetU32(file, FrameCountAt, uint.MaxValue);
        AssertError(file, "offset table");
    }

    [Fact]
    public void RateNumerator_Zero()
    {
        var file = ThreeFrames();
        SetU32(file, RateNumeratorAt, 0);
        AssertError(file, "frame rate numerator");
    }

    [Fact]
    public void RateDenominator_Zero()
    {
        var file = ThreeFrames();
        SetU32(file, RateDenominatorAt, 0);
        AssertError(file, "frame rate denominator");
    }

    [Fact]
    public void Offsets_NotIncreasing_Equal()
    {
        var file = ThreeFrames();
        SetU32(file, TableEntryAt(2), BitConverter.ToUInt32(file, TableEntryAt(1)));
        AssertError(file, "offset table");
    }

    [Fact]
    public void Offsets_NotIncreasing_Decreasing()
    {
        var file = ThreeFrames();
        SetU32(file, TableEntryAt(2), BitConverter.ToUInt32(file, TableEntryAt(1)) - 1);
        AssertError(file, "offset table");
    }

    [Fact]
    public void Offsets_FirstInsideTheTable()
    {
        var file = ThreeFrames();
        SetU32(file, TableEntryAt(0), HeaderSize);
        AssertError(file, "offset table");
    }

    [Fact]
    public void LastFrame_OverflowsTheFile()
    {
        var file = ThreeFrames();
        SetU32(file, TableEntryAt(3), (uint)file.Length + 1);
        var exception = Assert.Throws<InvalidDataException>(() => Read(file));
        Assert.Contains(Name, exception.Message);
        Assert.Contains("offset table", exception.Message);
        Assert.Contains("overflows", exception.Message);
    }

    [Fact]
    public void Audio_OffsetWithoutLength()
    {
        var file = ThreeFrames();
        SetU32(file, AudioOffsetAt, 100);
        AssertError(file, "audio section");
    }

    [Fact]
    public void Audio_OutsideTheFile()
    {
        var file = ThreeFrames(new byte[] { 1, 2, 3, 4 });
        SetU32(file, AudioLengthAt, 5);
        AssertError(file, "audio section");
    }

    [Fact]
    public void Audio_OverlapsTheFrames()
    {
        var file = ThreeFrames(new byte[] { 1, 2, 3, 4 });
        SetU32(file, AudioOffsetAt, BitConverter.ToUInt32(file, AudioOffsetAt) - 1);
        SetU32(file, AudioLengthAt, 4);
        AssertError(file, "audio section");
    }

    [Fact]
    public void Audio_OffsetOverflowingU32_IsOutsideTheFile()
    {
        var file = ThreeFrames(new byte[] { 1, 2, 3, 4 });
        SetU32(file, AudioOffsetAt, uint.MaxValue);
        SetU32(file, AudioLengthAt, 4);
        AssertError(file, "audio section");
    }
}
