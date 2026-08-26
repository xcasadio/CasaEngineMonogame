using CasaEngine.Framework.Audio;

namespace CasaEngine.Tests.Audio;

/// <summary>In-memory <see cref="IAudioClipProvider"/>: no catalogue, no file system.</summary>
public sealed class FakeAudioClipProvider : IAudioClipProvider
{
    private readonly Dictionary<Guid, IAudioClip> _clips = new();
    private readonly Dictionary<Guid, byte[]> _streams = new();

    public Guid Register(IAudioClip clip)
    {
        var id = Guid.NewGuid();
        _clips.Add(id, clip);
        return id;
    }

    public Guid RegisterStream(byte[] content)
    {
        var id = Guid.NewGuid();
        _streams.Add(id, content);
        return id;
    }

    public IAudioClip GetClip(Guid audioFileAssetId)
    {
        return _clips.GetValueOrDefault(audioFileAssetId);
    }

    public Stream OpenStream(Guid audioFileAssetId)
    {
        return _streams.TryGetValue(audioFileAssetId, out var content)
            ? new MemoryStream(content, writable: false)
            : null;
    }
}
