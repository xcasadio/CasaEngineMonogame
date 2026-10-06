using CasaEngine.EditorServices;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class AudioMixerAssetEditorSerializationTests
{
    private static AudioMixerAsset BuildAsset()
    {
        var asset = new AudioMixerAsset { Name = "Project mixer" };

        var music = new AudioMixerBusData { Name = "Music", Volume = 0.8f };
        music.Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.HighPass, 220f, 1.5f, 3f));
        music.Effects.Add(new AudioMixerCompressorEffectData(-20f, 3f, 4f, 0.02f, 0.2f, 1.5f));
        music.Effects.Add(new AudioMixerDuckingEffectData("Voice", 9f, -35f, 0.05f, 0.5f));
        music.Sends.Add(new AudioMixerSendData("Reverb", 0.3f));
        asset.Buses.Add(music);

        var reverb = new AudioMixerBusData { Name = "Reverb", Parent = "Sfx", Volume = 0.6f };
        reverb.Effects.Add(new AudioMixerReverbEffectData(0.7f, 0.2f, 0.9f, 0.1f, 0.5f));
        asset.Buses.Add(reverb);

        asset.Buses.Add(new AudioMixerBusData { Name = "Voice" });
        return asset;
    }

    private static JObject Serialize(AudioMixerAsset asset)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));
        return document;
    }

    private static string[] Keys(JToken token)
        => ((JObject)token).Properties().Select(property => property.Name).ToArray();

    [Fact]
    public void TrySerialize_WritesTheVersionKeysAndSnakeCaseFields()
    {
        var asset = BuildAsset();

        var document = Serialize(asset);

        Assert.Equal(asset.Id.ToString(), (string)document["id"]);
        Assert.Equal("Project mixer", (string)document["name"]);
        Assert.Equal("AudioMixerAsset", (string)document["type"]);
        Assert.Equal(1, (int)document["version"]);
        Assert.Equal(1, (int)document["schema_version"]);

        var music = document["buses"][0];
        Assert.Equal(new[] { "name", "parent", "volume", "effects", "sends" }, Keys(music));
        var effects = music["effects"];
        Assert.Equal(new[] { "type", "filter", "frequency_hz", "q", "gain_db" }, Keys(effects[0]));
        Assert.Equal("HighPass", (string)effects[0]["filter"]);
        Assert.Equal(
            new[] { "type", "threshold_db", "ratio", "knee_db", "attack_seconds", "release_seconds", "makeup_gain_db" },
            Keys(effects[1]));
        Assert.Equal(
            new[] { "type", "source", "depth_db", "threshold_db", "attack_seconds", "release_seconds" },
            Keys(effects[2]));
        Assert.Equal(
            new[] { "type", "room_size", "damping", "wet", "dry", "stereo_separation" },
            Keys(document["buses"][1]["effects"][0]));
        Assert.Equal(new[] { "target", "level" }, Keys(music["sends"][0]));
    }

    [Fact]
    public void RoundTrip_KeepsEveryFieldOfEveryEffectKind()
    {
        var asset = BuildAsset();
        var loaded = new AudioMixerAsset();

        loaded.Load(Serialize(asset));

        Assert.Equal(asset.Id, loaded.Id);
        Assert.Equal(asset.Name, loaded.Name);
        Assert.Equal(asset.Buses.Count, loaded.Buses.Count);
        for (int index = 0; index < asset.Buses.Count; index++)
        {
            var expected = asset.Buses[index];
            var actual = loaded.Buses[index];
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Parent, actual.Parent);
            Assert.Equal(expected.Volume, actual.Volume);
            Assert.Equal(expected.Effects, actual.Effects);
            Assert.Equal(expected.Sends, actual.Sends);
        }
    }

    [Fact]
    public void RoundTrip_CreateDefault()
    {
        var asset = AudioMixerAsset.CreateDefault("Default");
        var loaded = new AudioMixerAsset();

        loaded.Load(Serialize(asset));

        Assert.Equal(new[] { "Music", "Sfx", "Voice", "Ui" }, loaded.Buses.Select(bus => bus.Name).ToArray());
        Assert.All(loaded.Buses, bus => Assert.Equal("Master", bus.Parent));
    }

    [Fact]
    public void EditorAssetSaveSource_AudioMixerPanelIsAppendedLast()
    {
        Assert.Equal(7, (int)EditorAssetSaveSource.AudioMixerEditorPanel);
        Assert.Equal(6, (int)EditorAssetSaveSource.SoundEditorPanel);
    }
}
