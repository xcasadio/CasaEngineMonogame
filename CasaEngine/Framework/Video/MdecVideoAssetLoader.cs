using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;

namespace CasaEngine.Framework.Video;

/// <summary>
/// Loads a <c>.mdecvideo</c> container as a <see cref="MdecVideoAsset"/> (ADR-0128). Only the header and the offset
/// table are read; a player opens the file again to read frames. An invalid container throws an exception naming
/// the file and the field.
/// </summary>
public sealed class MdecVideoAssetLoader : IAssetLoader
{
    public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        => MdecVideoAsset.Load(fileName);

    public bool IsFileSupported(string fileName)
        => Path.GetExtension(fileName).Equals(Constants.FileNameExtensions.MdecVideo, StringComparison.OrdinalIgnoreCase);
}
