using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class SoundAssetTests
{
    private static JObject CreateDocument(Guid id, Guid audioFileAssetId)
    {
        return new JObject
        {
            ["id"] = id.ToString(),
            ["name"] = "footstep",
            ["audio_file_asset_id"] = audioFileAssetId.ToString(),
            ["volume"] = 0.75f,
            ["pitch"] = -0.25f,
            ["is_looped"] = true,
            ["bus_name"] = AudioBusNames.Ui,
            ["is_streaming"] = true,
        };
    }

    [Fact]
    public void Load_ReadsEveryField()
    {
        var id = Guid.NewGuid();
        var audioFileAssetId = Guid.NewGuid();
        var asset = new SoundAsset();

        asset.Load(CreateDocument(id, audioFileAssetId));

        Assert.Equal(id, asset.Id);
        Assert.Equal("footstep", asset.Name);
        Assert.Equal(audioFileAssetId, asset.AudioFileAssetId);
        Assert.Equal(0.75f, asset.Volume, 4);
        Assert.Equal(-0.25f, asset.Pitch, 4);
        Assert.True(asset.IsLooped);
        Assert.Equal(AudioBusNames.Ui, asset.BusName);
        Assert.True(asset.IsStreaming);
    }

    [Fact]
    public void Load_AppliesTheDefaultsOnAMinimalDocument()
    {
        var asset = new SoundAsset();

        asset.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "minimal",
        });

        Assert.Equal(Guid.Empty, asset.AudioFileAssetId);
        Assert.Equal(1f, asset.Volume);
        Assert.Equal(0f, asset.Pitch);
        Assert.False(asset.IsLooped);
        Assert.Equal(AudioBusNames.Sfx, asset.BusName);
        Assert.False(asset.IsStreaming);
    }

    [Fact]
    public void NewAsset_IsPlayableAsIs()
    {
        var asset = new SoundAsset();

        Assert.Equal(1f, asset.Volume);
        Assert.Equal(AudioBusNames.Sfx, asset.BusName);
        Assert.Equal(AudioVoiceParameters.Default, asset.CreateVoiceParameters());
    }

    [Theory]
    [InlineData(4f, 1f)]
    [InlineData(-1f, 0f)]
    public void Volume_IsClamped(float value, float expected)
    {
        var asset = new SoundAsset { Volume = value };

        Assert.Equal(expected, asset.Volume);
    }

    [Theory]
    [InlineData(9f, 1f)]
    [InlineData(-9f, -1f)]
    public void Pitch_IsClamped(float value, float expected)
    {
        var asset = new SoundAsset { Pitch = value };

        Assert.Equal(expected, asset.Pitch);
    }

    [Fact]
    public void NaN_KeepsThePreviousValue()
    {
        var asset = new SoundAsset { Volume = 0.5f, Pitch = 0.25f };

        asset.Volume = float.NaN;
        asset.Pitch = float.NaN;

        Assert.Equal(0.5f, asset.Volume);
        Assert.Equal(0.25f, asset.Pitch);
    }

    [Fact]
    public void EmptyBusName_FallsBackToSfx()
    {
        var asset = new SoundAsset { BusName = "   " };

        Assert.Equal(AudioBusNames.Sfx, asset.BusName);
    }

    [Fact]
    public void CreateVoiceParameters_MirrorsTheAsset()
    {
        var asset = new SoundAsset
        {
            Volume = 0.5f,
            Pitch = 0.25f,
            IsLooped = true,
        };

        var parameters = asset.CreateVoiceParameters();

        Assert.Equal(0.5f, parameters.Volume, 4);
        Assert.Equal(0.25f, parameters.Pitch, 4);
        Assert.Equal(0f, parameters.Pan);
        Assert.True(parameters.IsLooped);
    }

    [Theory]
    [InlineData("weapon.sound", true)]
    [InlineData("weapon.SOUND", true)]
    [InlineData("weapon.wav", false)]
    [InlineData("weapon.particle", false)]
    public void Loader_OnlySupportsTheSoundExtension(string fileName, bool expected)
    {
        Assert.Equal(expected, new SoundAssetLoader().IsFileSupported(fileName));
    }

    [Fact]
    public void Loader_ReturnsNullInsteadOfThrowingOnABrokenDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"casaengine-broken-{Guid.NewGuid():N}.sound");
        File.WriteAllText(path, "{ this is not json");

        try
        {
            Assert.Null(new SoundAssetLoader().LoadAsset(path, null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Loader_ReadsARealDocumentFromDisk()
    {
        var id = Guid.NewGuid();
        var audioFileAssetId = Guid.NewGuid();
        var path = Path.Combine(Path.GetTempPath(), $"casaengine-{Guid.NewGuid():N}.sound");
        File.WriteAllText(path, CreateDocument(id, audioFileAssetId).ToString());

        try
        {
            var asset = Assert.IsType<SoundAsset>(new SoundAssetLoader().LoadAsset(path, null));

            Assert.Equal(id, asset.Id);
            Assert.Equal(audioFileAssetId, asset.AudioFileAssetId);
            Assert.True(asset.IsStreaming);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
