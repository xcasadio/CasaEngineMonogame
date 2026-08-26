namespace CasaEngine.Framework.Audio;

/// <summary>
/// Resolves the audio file referenced by a <see cref="SoundAsset"/>.
/// </summary>
/// <remarks>
/// Indirection on purpose: in the game this is backed by the asset content manager, while tests
/// hand out in-memory clips. It keeps <see cref="AudioService"/> free of the asset pipeline.
/// </remarks>
public interface IAudioClipProvider
{
    /// <summary>Returns the clip for that asset id, or null when it cannot be resolved.</summary>
    IAudioClip GetClip(Guid audioFileAssetId);

    /// <summary>Opens a readable stream on the audio file, or null when it cannot be resolved.</summary>
    Stream OpenStream(Guid audioFileAssetId);
}
