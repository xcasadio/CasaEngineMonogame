using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
using Newtonsoft.Json.Linq;
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

    private static readonly string[] SixKeys =
    {
        "audio_file_asset_id", "volume", "pitch", "is_looped", "bus_name", "is_streaming",
    };

    private static JObject Serialize(SoundAsset asset)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));
        return document;
    }

    private static string DemoAudioDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "CasaEngine.Demos", "Content", "Audio");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine.Demos/Content/Audio not found above the test output.");
    }

    public static IEnumerable<object[]> DemoSoundFiles()
    {
        foreach (var path in Directory.GetFiles(DemoAudioDirectory(), "*.sound"))
        {
            yield return new object[] { Path.GetFileName(path) };
        }
    }

    [Fact]
    public void SaveThenLoad_KeepsEveryVariationField()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var saved = new SoundAsset
        {
            Priority = 55,
            VariationVolumeMin = 0.5f,
            VariationVolumeMax = 0.8f,
            VariationPitchMin = -0.25f,
            VariationPitchMax = 0.125f,
        };
        saved.VariationAudioFileAssetIds.Add(first);
        saved.VariationAudioFileAssetIds.Add(second);

        var loaded = new SoundAsset();
        loaded.Load(Serialize(saved));

        Assert.Equal(55, loaded.Priority);
        Assert.Equal(new[] { first, second }, loaded.VariationAudioFileAssetIds);
        Assert.Equal(0.5f, loaded.VariationVolumeMin, 4);
        Assert.Equal(0.8f, loaded.VariationVolumeMax, 4);
        Assert.Equal(-0.25f, loaded.VariationPitchMin, 4);
        Assert.Equal(0.125f, loaded.VariationPitchMax, 4);
    }

    [Fact]
    public void Serialize_OfANewAsset_WritesNoVariationKey()
    {
        var document = Serialize(new SoundAsset());

        foreach (var key in new[]
                 {
                     "priority", "variation_audio_file_asset_ids", "variation_volume_min",
                     "variation_volume_max", "variation_pitch_min", "variation_pitch_max",
                 })
        {
            Assert.False(document.ContainsKey(key), key);
        }
    }

    [Theory]
    [MemberData(nameof(DemoSoundFiles))]
    public void Serialize_OfADemoSoundFile_KeepsExactlyItsOriginalKeys(string fileName)
    {
        var path = Path.Combine(DemoAudioDirectory(), fileName);
        var original = JObject.Parse(File.ReadAllText(path));
        var asset = Assert.IsType<SoundAsset>(new SoundAssetLoader().LoadAsset(path, null));

        var document = Serialize(asset);

        Assert.Equal(
            original.Properties().Select(p => p.Name).OrderBy(n => n),
            document.Properties().Select(p => p.Name).OrderBy(n => n));
        foreach (var key in SixKeys)
        {
            Assert.True(document.ContainsKey(key));
        }
    }

    [Fact]
    public void Serialize_WritesOnlyTheVariationKeyThatDiffersFromItsDefault()
    {
        var document = Serialize(new SoundAsset { VariationVolumeMin = 0.5f, VariationVolumeMax = 1f });

        Assert.True(document.ContainsKey("variation_volume_min"));
        Assert.False(document.ContainsKey("variation_volume_max"));
        Assert.False(document.ContainsKey("variation_pitch_min"));
        Assert.False(document.ContainsKey("variation_pitch_max"));
        Assert.False(document.ContainsKey("priority"));
    }

    [Fact]
    public void Serialize_DoesNotWriteAnEmptyGuidVariationEntry()
    {
        var kept = Guid.NewGuid();
        var asset = new SoundAsset();
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);
        asset.VariationAudioFileAssetIds.Add(kept);

        var array = Assert.IsType<JArray>(Serialize(asset)["variation_audio_file_asset_ids"]);

        Assert.Equal(new[] { kept.ToString() }, array.Select(t => (string)t));

        var onlyEmpty = new SoundAsset();
        onlyEmpty.VariationAudioFileAssetIds.Add(Guid.Empty);
        Assert.False(Serialize(onlyEmpty).ContainsKey("variation_audio_file_asset_ids"));
    }

    [Fact]
    public void SaveLoadSave_ProducesEqualDocuments()
    {
        var asset = new SoundAsset { Priority = 7, VariationPitchMin = -0.3f, VariationPitchMax = 0.3f };
        asset.VariationAudioFileAssetIds.Add(Guid.NewGuid());
        var first = Serialize(asset);

        var reloaded = new SoundAsset();
        reloaded.Load(first);
        var second = Serialize(reloaded);

        Assert.True(JToken.DeepEquals(first, second));
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

    private static readonly string[] SpatialKeys =
    {
        "spatial_mode", "distance_model", "reference_distance", "max_distance", "rolloff_factor", "doppler_factor",
        "parameter_bindings",
    };

    [Fact]
    public void SaveThenLoad_KeepsEverySpatialField()
    {
        var saved = new SoundAsset
        {
            SpatialMode = AudioSpatialMode.Spatial3D,
            DistanceModel = AudioDistanceModel.ExponentDistanceClamped,
            ReferenceDistance = 2.5f,
            MaxDistance = 400f,
            RolloffFactor = 0.5f,
            DopplerFactor = 1.25f,
        };
        saved.SetParameterBindings(new[]
        {
            new AudioParameterBinding("speed", AudioParameterTarget.Volume, 0f, 10f, 1f, 0.25f),
            new AudioParameterBinding("rpm", AudioParameterTarget.Pitch, -1f, 1f, -0.5f, 0.5f),
        });

        var document = Serialize(saved);

        Assert.Equal("Spatial3D", (string)document["spatial_mode"]);
        Assert.Equal("ExponentDistanceClamped", (string)document["distance_model"]);
        var bindingsNode = Assert.IsType<JArray>(document["parameter_bindings"]);
        Assert.Equal(
            new[] { "input_max", "input_min", "output_max", "output_min", "parameter", "target" },
            ((JObject)bindingsNode[0]).Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        var loaded = new SoundAsset();
        loaded.Load(document);

        Assert.Equal(saved.SpatialMode, loaded.SpatialMode);
        Assert.Equal(saved.DistanceModel, loaded.DistanceModel);
        Assert.Equal(2.5f, loaded.ReferenceDistance);
        Assert.Equal(400f, loaded.MaxDistance);
        Assert.Equal(0.5f, loaded.RolloffFactor);
        Assert.Equal(1.25f, loaded.DopplerFactor);
        Assert.Equal(2, loaded.ParameterBindings.Count);
        Assert.Equal("rpm", loaded.ParameterBindings[1].ParameterName);
        Assert.Equal(AudioParameterTarget.Pitch, loaded.ParameterBindings[1].Target);
        Assert.Equal(-1f, loaded.ParameterBindings[1].InputMin);
        Assert.Equal(1f, loaded.ParameterBindings[1].InputMax);
        Assert.Equal(-0.5f, loaded.ParameterBindings[1].OutputMin);
        Assert.Equal(0.5f, loaded.ParameterBindings[1].OutputMax);
        Assert.True(JToken.DeepEquals(document, Serialize(loaded)));
    }

    [Fact]
    public void Serialize_OfANewAsset_WritesNoSpatialKey()
    {
        var document = Serialize(new SoundAsset());

        foreach (var key in SpatialKeys)
        {
            Assert.False(document.ContainsKey(key), key);
        }
    }

    [Fact]
    public void Serialize_WritesOnlyTheSpatialKeyThatDiffersFromItsDefault()
    {
        var document = Serialize(new SoundAsset { DistanceModel = AudioDistanceModel.LinearDistance, MaxDistance = float.PositiveInfinity });

        Assert.Equal("LinearDistance", (string)document["distance_model"]);
        foreach (var key in SpatialKeys.Where(key => key != "distance_model"))
        {
            Assert.False(document.ContainsKey(key), key);
        }
    }
}
