using CasaEngine.EditorServices;
using CasaEngine.Framework.UI.MGUI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Pins the optional <c>design_time_data_file</c> field of the <c>.uiscreen</c> envelope (ADR-0038): an
/// envelope that never had it loads and saves unchanged, and the field round-trips when present.
/// </summary>
public class UIScreenAssetSerializationTests
{
    private const string EnvelopeWithoutDesignTimeData = """
        {
          "id": "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234",
          "name": "MainMenu",
          "source_xaml_file": "MainMenu.xaml",
          "theme_name": "Default",
          "preview_resolution": { "x": 1280, "y": 720 },
          "resource_files": ["Shared.xaml"]
        }
        """;

    [Fact]
    public void AnEnvelopeWithoutTheField_Loads_WithAnEmptyDefault()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.Equal(string.Empty, asset.DesignTimeDataFile);
        // Every other optional field still loads the same way.
        Assert.Equal("MainMenu.xaml", asset.SourceXamlFile);
        Assert.Equal("Default", asset.ThemeName);
        Assert.Equal(1280, asset.PreviewResolution.X);
        Assert.Equal(720, asset.PreviewResolution.Y);
        Assert.Equal(new[] { "Shared.xaml" }, asset.ResourceFiles);
    }

    [Fact]
    public void AnEnvelopeWithoutTheField_SavedAgain_DoesNotGainIt()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));

        Assert.False(document.ContainsKey("design_time_data_file"));
    }

    [Fact]
    public void ANewAsset_WithNoDesignTimeDataFile_IsNotSerialized()
    {
        var asset = new UIScreenAsset();

        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));

        Assert.False(document.ContainsKey("design_time_data_file"));
    }

    [Fact]
    public void TheField_RoundTrips_ThroughLoadAndSave()
    {
        var saved = new UIScreenAsset
        {
            SourceXamlFile = "MainMenu.xaml",
            DesignTimeDataFile = "MainMenu.designtime.json",
        };

        Assert.True(EditorAssetJsonSerializer.TrySerialize(saved, out var document));
        Assert.Equal("MainMenu.designtime.json", document["design_time_data_file"]?.Value<string>());

        var loaded = new UIScreenAsset();
        loaded.Load(document);

        Assert.Equal("MainMenu.designtime.json", loaded.DesignTimeDataFile);

        // And a second round trip keeps it, the way any other field would.
        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var secondDocument));
        var reloaded = new UIScreenAsset();
        reloaded.Load(secondDocument);
        Assert.Equal("MainMenu.designtime.json", reloaded.DesignTimeDataFile);
    }
}
