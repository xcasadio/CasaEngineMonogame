using System.Text.Json;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093, task T2.5: sound creation, the typed and bounded sound settings (P5), no history.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteSoundCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-sound-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();
    private readonly Guid _soundId;

    public EditorRemoteSoundCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "project", "Sounds"));
        _host = new RemoteTestHost { ProjectDirectory = Path.Combine(_directory, "project"), SoundAsset = new SoundAsset { Name = "Step" } };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));
        _soundId = Catalog("Sounds/Step.sound");
    }

    public void Dispose()
    {
        _control.Dispose();
        foreach (Guid id in _catalogEntries)
        {
            AssetCatalog.RemoveInternal(id, out _);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private Guid Catalog(string fileName)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, "remote-sound-test-" + id.ToString("N"), fileName);
        _catalogEntries.Add(id);
        string path = Path.Combine(_host.ProjectDirectory!, fileName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return id;
    }

    private RemoteResponse Call(string method, object parameters = null)
    {
        var call = new EditorRemoteCall(new RemoteRequest
        {
            Id = 1,
            Method = method,
            Params = parameters == null ? null : RemoteMessageCodec.ToElement(parameters),
        });
        _control.Dispatcher.Enqueue(call);
        _control.Pump();
        Assert.True(call.IsCompleted);
        return call.Completion.Result;
    }

    private static JsonElement Ok(RemoteResponse response)
    {
        Assert.True(response.Error == null, response.Error?.ToString());
        return response.Result!.Value;
    }

    private RemoteResponse Set(string property, object value)
        => Call("sound.set_property", new { asset = _soundId.ToString(), property, value });

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Create_GoesThroughTheChecksOfP4()
    {
        string name = "RemoteStep" + Guid.NewGuid().ToString("N")[..8];

        Ok(Call("sound.create", new { name, folder = "Sounds" }));

        Assert.Equal("Sounds" + Path.DirectorySeparatorChar + name + ".sound", Assert.Single(_host.CreatedSounds));
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("sound.create", new { name = "X", folder = "../out" }).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Call("sound.create", new { name = "bad/name", folder = "Sounds" }).Error!.Code);
    }

    [Fact]
    public void SetProperty_AppliesTypedValues_WithoutHistory()
    {
        Guid wav = Catalog("Audio/step.wav");
        Guid ogg = Catalog("Audio/step2.ogg");

        JsonElement result = Ok(Set("volume", 0.5));
        Ok(Set("priority", 40));
        Ok(Set("is_looped", true));
        Ok(Set("spatial_mode", AudioSpatialMode.Spatial3D.ToString().ToLowerInvariant()));
        Ok(Set("audio_file_asset_id", wav.ToString()));
        Ok(Set("variation_audio_file_asset_ids", new[] { ogg.ToString(), Guid.Empty.ToString() }));
        Ok(Set("bus_name", "music"));

        SoundAsset sound = _host.SoundAsset!;
        Assert.False(result.GetProperty("undoable").GetBoolean());
        Assert.Equal(0.5f, sound.Volume);
        Assert.Equal(40, sound.Priority);
        Assert.True(sound.IsLooped);
        Assert.Equal(AudioSpatialMode.Spatial3D, sound.SpatialMode);
        Assert.Equal(wav, sound.AudioFileAssetId);
        Assert.Equal(new[] { ogg, Guid.Empty }, sound.VariationAudioFileAssetIds);
        Assert.Equal("music", sound.BusName, ignoreCase: true);
        Assert.True(_host.SoundModified);
    }

    /// <summary>Security review S2-2: values outside the inspector fields, wrong JSON types, numeric enums, unknown keys.</summary>
    [Theory]
    [InlineData("volume", "1.5")]
    [InlineData("volume", "\"0.5\"")]
    [InlineData("pitch", "-2")]
    [InlineData("priority", "101")]
    [InlineData("priority", "2.5")]
    [InlineData("exclusion_fade_seconds", "11")]
    [InlineData("max_distance", "100001")]
    [InlineData("rolloff_factor", "1e40")]
    [InlineData("is_looped", "1")]
    [InlineData("spatial_mode", "1")]
    [InlineData("distance_model", "\"Nope\"")]
    [InlineData("bus_name", "\"Master\"")]
    [InlineData("bus_name", "\"NoSuchBus\"")]
    [InlineData("audio_file_asset_id", "\"not-a-guid\"")]
    [InlineData("variation_audio_file_asset_ids", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("loop_start_sample", "0")]
    [InlineData("name", "\"x\"")]
    public void Validate_RefusesValuesOutsideTheInspectorBounds(string property, string json)
    {
        Assert.NotNull(EditorRemoteSoundCommands.Validate(property, Json(json)));
    }

    [Fact]
    public void Validate_AudioFilesMustBeWavOrOggOfTheCatalog()
    {
        Guid material = Catalog("Materials/Stone.material");

        Assert.NotNull(EditorRemoteSoundCommands.Validate("audio_file_asset_id", Json($"\"{material}\"")));
        Assert.NotNull(EditorRemoteSoundCommands.Validate("audio_file_asset_id", Json($"\"{Guid.NewGuid()}\"")));
        Assert.Null(EditorRemoteSoundCommands.Validate("audio_file_asset_id", Json($"\"{Guid.Empty}\"")));
        string tooMany = "[" + string.Join(",", Enumerable.Repeat($"\"{Guid.Empty}\"", EditorRemoteSoundCommands.MaxVariationFiles + 1)) + "]";
        Assert.NotNull(EditorRemoteSoundCommands.Validate("variation_audio_file_asset_ids", Json(tooMany)));
    }

    [Fact]
    public void SetProperty_RefusedValueLeavesTheSoundUnchanged()
    {
        Assert.Equal(RemoteErrorCodes.InvalidParams, Set("volume", 2).Error!.Code);

        Assert.Equal(1f, _host.SoundAsset!.Volume);
        Assert.False(_host.SoundModified);
    }

    [Fact]
    public void Get_ReadsTheSettingsAndTheModifiedState()
    {
        _host.SoundAsset!.DistanceModel = AudioDistanceModel.LinearDistance;
        _host.SoundModified = true;

        JsonElement result = Ok(Call("sound.get", new { asset = _soundId.ToString() }));

        Assert.Equal(nameof(AudioDistanceModel.LinearDistance), result.GetProperty("distance_model").GetString());
        Assert.True(result.GetProperty("max_distance_unlimited").GetBoolean());
        Assert.True(result.GetProperty("modified").GetBoolean());
        Assert.Contains(result.GetProperty("keys").EnumerateArray(), key => key.GetString() == "bus_name");
    }

    [Fact]
    public void Save_GoesThroughThePanel()
    {
        Ok(Call("sound.save", new { asset = _soundId.ToString() }));

        Assert.Equal(_soundId, Assert.Single(_host.SavedAssets));
    }

    [Fact]
    public void PanelClose_RefusesAModifiedSoundPanel()
    {
        _host.Panels.Add(new RemotePanelInfo { Id = "panel_sound_test", Title = "Step", Kind = "sound", Modified = true });

        Assert.Equal(RemoteErrorReasons.UnsavedChanges, Call("panel.close", new { panel_id = "panel_sound_test" }).Error!.Data!.Reason);
        Assert.Empty(_host.ClosedPanels);
    }
}
