using CasaEngine.EditorServices;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class SoundAssetEditorSerializationTests
{
    [Fact]
    public void SaveThenLoad_KeepsEveryField()
    {
        var audioFileAssetId = Guid.NewGuid();
        var saved = new SoundAsset
        {
            Name = "sword_swing",
            AudioFileAssetId = audioFileAssetId,
            Volume = 0.4f,
            Pitch = -0.6f,
            IsLooped = true,
            BusName = AudioBusNames.Voice,
            IsStreaming = true,
        };

        Assert.True(EditorAssetJsonSerializer.TrySerialize(saved, out var document));

        var loaded = new SoundAsset();
        loaded.Load(document);

        Assert.Equal(saved.Id, loaded.Id);
        Assert.Equal(saved.Name, loaded.Name);
        Assert.Equal(audioFileAssetId, loaded.AudioFileAssetId);
        Assert.Equal(saved.Volume, loaded.Volume, 4);
        Assert.Equal(saved.Pitch, loaded.Pitch, 4);
        Assert.Equal(saved.IsLooped, loaded.IsLooped);
        Assert.Equal(saved.BusName, loaded.BusName);
        Assert.Equal(saved.IsStreaming, loaded.IsStreaming);
    }

    [Fact]
    public void SaveThenLoad_OfANewAsset_KeepsTheDefaults()
    {
        var saved = new SoundAsset();

        Assert.True(EditorAssetJsonSerializer.TrySerialize(saved, out var document));

        var loaded = new SoundAsset();
        loaded.Load(document);

        Assert.Equal(Guid.Empty, loaded.AudioFileAssetId);
        Assert.Equal(1f, loaded.Volume);
        Assert.Equal(0f, loaded.Pitch);
        Assert.False(loaded.IsLooped);
        Assert.Equal(AudioBusNames.Sfx, loaded.BusName);
        Assert.False(loaded.IsStreaming);
    }

    [Fact]
    public void Serialize_UsesTheSnakeCaseFieldNamesTheRuntimeReads()
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(new SoundAsset(), out var document));

        Assert.True(document.ContainsKey("audio_file_asset_id"));
        Assert.True(document.ContainsKey("volume"));
        Assert.True(document.ContainsKey("pitch"));
        Assert.True(document.ContainsKey("is_looped"));
        Assert.True(document.ContainsKey("bus_name"));
        Assert.True(document.ContainsKey("is_streaming"));
    }
}
