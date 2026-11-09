using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Video;
using CasaEngine.Framework.Video.Mdec;

namespace CasaEngine.Tests.Video;

/// <summary>Small <c>.mdecvideo</c> files and the asset loader that serves them, for the tests of the world video component.</summary>
internal static class WorldVideoTestFiles
{
    public const int Size = 16;

    private static byte[] Frame(int index)
    {
        var luma = -300 + index * 20;
        var blocks = new[]
        {
            new MdecTestBlock(index % 5 * 8 - 16),
            new MdecTestBlock(16 - index % 3 * 8),
            new MdecTestBlock(luma),
            new MdecTestBlock(luma),
            new MdecTestBlock(luma),
            new MdecTestBlock(luma),
        };
        return MdecTestEncoder.Encode(1 + index % 3, index % 4 + 1, blocks);
    }

    /// <summary>Writes a 16 x 16 video of flat-colour frames and returns its path. The caller deletes it.</summary>
    public static string Write(int frameCount, uint rateNumerator, uint rateDenominator, int corruptFrame = -1)
    {
        var frames = new List<byte[]>();
        for (var i = 0; i < frameCount; i++)
        {
            frames.Add(Frame(i));
        }

        var path = Path.Combine(Path.GetTempPath(), $"world-video-{Guid.NewGuid():N}.mdecvideo");
        var bytes = MdecTestContainerWriter.Write(frames, Size, Size, rateNumerator, rateDenominator);
        if (corruptFrame >= 0)
        {
            var asset = MdecVideoAsset.Read(new MemoryStream(bytes), "corrupt.mdecvideo");
            bytes[(int)asset.GetFrameOffset(corruptFrame) + 2] ^= 0xFF;
        }

        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Serves the container at a fixed path for every asset id; counts the loads.</summary>
    public sealed class Loader : IAssetLoader
    {
        private readonly string _path;
        public int Loads;

        public Loader(string path) => _path = path;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return MdecVideoAsset.Load(_path);
        }

        public bool IsFileSupported(string fileName) => true;
    }
}
