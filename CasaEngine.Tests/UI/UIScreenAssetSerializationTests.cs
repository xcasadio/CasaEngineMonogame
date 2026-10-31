using CasaEngine.EditorServices;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.UI.MGUI;
using Microsoft.Xna.Framework;
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

    private const string EnvelopeWithCommands = """
        {
          "id": "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234",
          "name": "MainMenu",
          "source_xaml_file": "MainMenu.xaml",
          "is_modal": true,
          "default_focus": "btnPlay",
          "ui_commands": [
            {
              "name": "Play",
              "actions": [
                { "type": "play_sound", "target": "Confirm" },
                { "type": "open_screen", "target": "Options" },
                { "type": "change_world", "target": "Level1.world" },
                { "type": "close_screen" },
                { "type": "quit" }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void AnEnvelopeWithoutTheCommandFields_Loads_WithEmptyDefaults()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.False(asset.IsModal);
        Assert.Equal(string.Empty, asset.DefaultFocus);
        Assert.Empty(asset.UICommands);
        Assert.Empty(asset.CommandFormatErrors);
    }

    [Fact]
    public void AnEnvelopeWithoutTheCommandFields_SavedAgain_DoesNotGainThem()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));

        Assert.False(document.ContainsKey("is_modal"));
        Assert.False(document.ContainsKey("default_focus"));
        Assert.False(document.ContainsKey("ui_commands"));
    }

    [Fact]
    public void TheCommandFields_Load_InTheirWrittenOrder()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithCommands));

        Assert.True(asset.IsModal);
        Assert.Equal("btnPlay", asset.DefaultFocus);
        Assert.Empty(asset.CommandFormatErrors);

        var command = Assert.Single(asset.UICommands);
        Assert.Equal("Play", command.Name);
        Assert.Equal(
            new[]
            {
                (UIScreenActionType.PlaySound, "Confirm"),
                (UIScreenActionType.OpenScreen, "Options"),
                (UIScreenActionType.ChangeWorld, "Level1.world"),
                (UIScreenActionType.CloseScreen, string.Empty),
                (UIScreenActionType.Quit, string.Empty),
            },
            command.Actions.Select(action => (action.Type, action.Target)));
    }

    [Fact]
    public void TheCommandFields_RoundTrip_ThroughLoadAndSave()
    {
        var original = JObject.Parse(EnvelopeWithCommands);
        var loaded = new UIScreenAsset();
        loaded.Load(original);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));

        Assert.True(JToken.DeepEquals(original["is_modal"], document["is_modal"]));
        Assert.True(JToken.DeepEquals(original["default_focus"], document["default_focus"]));
        Assert.True(JToken.DeepEquals(original["ui_commands"], document["ui_commands"]));
    }

    [Fact]
    public void AnActionOfAnUnknownType_IsKept_WithEverythingItCarried()
    {
        var action = JObject.Parse("""{ "type": "shake_screen", "target": "x", "duration": 12, "from": { "a": 1 } }""");
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(action));

        var data = Assert.Single(Assert.Single(loaded.UICommands).Actions);
        Assert.Equal(UIScreenActionType.Unknown, data.Type);
        Assert.Empty(loaded.CommandFormatErrors);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.True(JToken.DeepEquals(action, document["ui_commands"]![0]!["actions"]![0]));
    }

    [Fact]
    public void AnActionWithNoType_IsUnknown_AndKept()
    {
        var action = JObject.Parse("""{ "target": "x" }""");
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(action));

        Assert.Equal(UIScreenActionType.Unknown, Assert.Single(Assert.Single(loaded.UICommands).Actions).Type);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.True(JToken.DeepEquals(action, document["ui_commands"]![0]!["actions"]![0]));
    }

    [Theory]
    [InlineData("""{ "is_modal": "yes" }""", "'is_modal'")]
    [InlineData("""{ "default_focus": 3 }""", "'default_focus'")]
    [InlineData("""{ "ui_commands": {} }""", "'ui_commands'")]
    [InlineData("""{ "ui_commands": [ 3 ] }""", "ui_commands[0]")]
    [InlineData("""{ "ui_commands": [ { "name": 3 } ] }""", "ui_commands[0].name")]
    [InlineData("""{ "ui_commands": [ { "name": "a", "actions": 3 } ] }""", "ui_commands[0].actions")]
    [InlineData("""{ "ui_commands": [ { "name": "a", "actions": [ 3 ] } ] }""", "ui_commands[0].actions[0]")]
    [InlineData("""{ "ui_commands": [ { "actions": [ { "type": 3 } ] } ] }""", "ui_commands[0].actions[0].type")]
    [InlineData("""{ "ui_commands": [ { "actions": [ { "type": "quit", "target": 3 } ] } ] }""", "ui_commands[0].actions[0].target")]
    public void AMalformedCommandField_DoesNotThrowFromLoad_AndIsReportedWithItsPlace(string json, string location)
    {
        var asset = new UIScreenAsset();

        asset.Load(WithIdentity(json));

        Assert.Contains(asset.CommandFormatErrors, error => error.Contains(location, StringComparison.Ordinal));
    }

    private const string FadeAction = """
        { "type": "fade_screen", "layer": "AboveUI", "blend_mode": "Subtractive", "from": [ 0, 0, 0 ], "to": [ 255, 128, 64 ], "duration_seconds": 0.25 }
        """;

    [Fact]
    public void AFade_Loads_EveryField()
    {
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(JObject.Parse(FadeAction)));

        Assert.Empty(loaded.CommandFormatErrors);

        var action = Assert.Single(Assert.Single(loaded.UICommands).Actions);
        Assert.Equal(UIScreenActionType.FadeScreen, action.Type);
        Assert.Equal(ScreenEffectLayer.AboveUI, action.Layer);
        Assert.Equal(SpriteBlendMode.Subtractive, action.BlendMode);
        Assert.Equal(new Color(0, 0, 0, 255), action.From);
        Assert.Equal(new Color(255, 128, 64, 255), action.To);
        Assert.Equal(0.25f, action.DurationSeconds);
    }

    [Fact]
    public void AFade_RoundTrips_ThroughLoadAndSave()
    {
        var action = JObject.Parse(FadeAction);
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(action));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));

        Assert.True(JToken.DeepEquals(action, document["ui_commands"]![0]!["actions"]![0]));
    }

    [Fact]
    public void AnAlphaFade_RoundTrips_WithItsFourthValue()
    {
        var action = JObject.Parse("""
            { "type": "fade_screen", "layer": "BelowUI", "blend_mode": "AlphaBlend", "from": [ 0, 0, 0, 0 ], "to": [ 0, 0, 0, 255 ], "duration_seconds": 2 }
            """);
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(action));

        var data = Assert.Single(Assert.Single(loaded.UICommands).Actions);
        Assert.Equal(new Color(0, 0, 0, 0), data.From);
        Assert.Equal(new Color(0, 0, 0, 255), data.To);
        Assert.Equal(2f, data.DurationSeconds);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));

        var written = document["ui_commands"]![0]!["actions"]![0]!;
        Assert.True(JToken.DeepEquals(action["from"], written["from"]));
        Assert.Equal(new JArray(0, 0, 0), written["to"], JToken.EqualityComparer);
    }

    [Fact]
    public void AWait_RoundTrips_WithItsTypeAlone()
    {
        var action = JObject.Parse("""{ "type": "wait_fade" }""");
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(action));

        Assert.Equal(UIScreenActionType.WaitFade, Assert.Single(Assert.Single(loaded.UICommands).Actions).Type);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.True(JToken.DeepEquals(action, document["ui_commands"]![0]!["actions"]![0]));
    }

    [Fact]
    public void AFadeWithNoFields_InventsNone()
    {
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(JObject.Parse("""{ "type": "fade_screen" }""")));

        Assert.Empty(loaded.CommandFormatErrors);

        var action = Assert.Single(Assert.Single(loaded.UICommands).Actions);
        Assert.Null(action.Layer);
        Assert.Null(action.BlendMode);
        Assert.Null(action.From);
        Assert.Null(action.To);
        Assert.Null(action.DurationSeconds);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.Equal(new[] { "type" }, ((JObject)document["ui_commands"]![0]!["actions"]![0]!).Properties().Select(property => property.Name));
    }

    [Fact]
    public void TheFadeFields_OfAnotherAction_AreNotRead()
    {
        var loaded = new UIScreenAsset();
        loaded.Load(EnvelopeWith(JObject.Parse("""{ "type": "quit", "layer": 3, "from": "x" }""")));

        Assert.Empty(loaded.CommandFormatErrors);
        Assert.Null(Assert.Single(Assert.Single(loaded.UICommands).Actions).Layer);
    }

    [Theory]
    [InlineData("""{ "layer": "Above" }""", ".layer")]
    [InlineData("""{ "layer": 1 }""", ".layer")]
    [InlineData("""{ "layer": "1" }""", ".layer")]
    [InlineData("""{ "layer": "aboveui" }""", ".layer")]
    [InlineData("""{ "blend_mode": "Multiply" }""", ".blend_mode")]
    [InlineData("""{ "blend_mode": 3 }""", ".blend_mode")]
    [InlineData("""{ "from": "black" }""", ".from")]
    [InlineData("""{ "from": [ 0, 0 ] }""", ".from")]
    [InlineData("""{ "from": [ 0, 0, 0, 0, 0 ] }""", ".from")]
    [InlineData("""{ "from": [ 0, 0, 256 ] }""", ".from")]
    [InlineData("""{ "to": [ -1, 0, 0 ] }""", ".to")]
    [InlineData("""{ "to": [ 0.5, 0, 0 ] }""", ".to")]
    [InlineData("""{ "to": [ "0", 0, 0 ] }""", ".to")]
    [InlineData("""{ "duration_seconds": "1" }""", ".duration_seconds")]
    public void AMalformedFadeField_IsReportedWithItsPlace(string field, string location)
    {
        var action = JObject.Parse("""{ "type": "fade_screen" }""");
        action.Merge(JObject.Parse(field));
        var loaded = new UIScreenAsset();

        loaded.Load(EnvelopeWith(action));

        Assert.Contains(loaded.CommandFormatErrors, error => error.Contains("actions[0]" + location, StringComparison.Ordinal));
    }

    [Fact]
    public void LoadingAgain_ForgetsThePreviousCommandFields()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithCommands));

        asset.Load(WithIdentity("""{ "is_modal": 3 }"""));
        Assert.Empty(asset.UICommands);
        Assert.False(asset.IsModal);
        Assert.Equal(string.Empty, asset.DefaultFocus);
        Assert.Single(asset.CommandFormatErrors);

        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));
        Assert.Empty(asset.CommandFormatErrors);
    }

    [Fact]
    public void AnEnvelopeWithoutFonts_LoadsAnEmptyList_AndDoesNotGainTheKeyWhenSaved()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.Empty(asset.Fonts);
        Assert.Empty(asset.CommandFormatErrors);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));
        Assert.False(document.ContainsKey("fonts"));
    }

    [Fact]
    public void Fonts_RoundTrip_InTheirWrittenOrder()
    {
        var original = WithIdentity("""{ "fonts": [ "title-font", "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901235" ] }""");
        var loaded = new UIScreenAsset();
        loaded.Load(original);

        Assert.Equal(new[] { "title-font", "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901235" }, loaded.Fonts);
        Assert.Empty(loaded.CommandFormatErrors);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.True(JToken.DeepEquals(original["fonts"], document["fonts"]));
    }

    [Fact]
    public void AnEmptyFontList_IsNotWrittenBack()
    {
        var loaded = new UIScreenAsset();
        loaded.Load(WithIdentity("""{ "fonts": [] }"""));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));

        Assert.False(document.ContainsKey("fonts"));
    }

    [Theory]
    [InlineData("""{ "fonts": "title-font" }""", "'fonts'")]
    [InlineData("""{ "fonts": {} }""", "'fonts'")]
    [InlineData("""{ "fonts": null }""", "'fonts'")]
    [InlineData("""{ "fonts": [ 3 ] }""", "fonts[0]")]
    [InlineData("""{ "fonts": [ "a", null ] }""", "fonts[1]")]
    public void AMalformedFontList_DoesNotThrowFromLoad_AndIsReportedWithItsPlace(string json, string location)
    {
        var asset = new UIScreenAsset();

        asset.Load(WithIdentity(json));

        Assert.Contains(asset.CommandFormatErrors, error => error.Contains(location, StringComparison.Ordinal));
    }

    [Fact]
    public void LoadingAgain_ForgetsThePreviousFonts()
    {
        var asset = new UIScreenAsset();
        asset.Load(WithIdentity("""{ "fonts": [ "title-font" ] }"""));

        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.Empty(asset.Fonts);
    }

    [Fact]
    public void AnEnvelopeWithoutTheNavigationFields_LoadsDefaults_AndDoesNotGainThemWhenSaved()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.Equal(UIScreenFocusWrap.None, asset.FocusWrap);
        Assert.Equal(string.Empty, asset.CancelCommand);
        Assert.Equal(string.Empty, asset.MoveCommand);
        Assert.Equal(string.Empty, asset.OnOpenCommand);
        Assert.Empty(asset.CommandFormatErrors);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var document));
        Assert.False(document.ContainsKey("focus_wrap"));
        Assert.False(document.ContainsKey("cancel_command"));
        Assert.False(document.ContainsKey("move_command"));
        Assert.False(document.ContainsKey("on_open_command"));
    }

    [Fact]
    public void TheOpenCommand_RoundTrips()
    {
        var original = WithIdentity("""{ "on_open_command": "Open" }""");

        var loaded = new UIScreenAsset();
        loaded.Load(original);
        Assert.Equal("Open", loaded.OnOpenCommand);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.True(JToken.DeepEquals(original["on_open_command"], document["on_open_command"]));
    }

    [Fact]
    public void AnOpenCommandThatIsNotAString_IsReported()
    {
        var asset = new UIScreenAsset();
        asset.Load(WithIdentity("""{ "on_open_command": 1 }"""));

        Assert.Contains(asset.CommandFormatErrors, error => error.Contains("'on_open_command'"));
    }

    [Theory]
    [InlineData("vertical", UIScreenFocusWrap.Vertical)]
    [InlineData("horizontal", UIScreenFocusWrap.Horizontal)]
    public void TheNavigationFields_RoundTrip(string name, UIScreenFocusWrap expected)
    {
        var original = WithIdentity(
            $$"""{ "focus_wrap": "{{name}}", "cancel_command": "Back", "move_command": "Tick" }""");
        var loaded = new UIScreenAsset();
        loaded.Load(original);

        Assert.Equal(expected, loaded.FocusWrap);
        Assert.Equal("Back", loaded.CancelCommand);
        Assert.Equal("Tick", loaded.MoveCommand);
        Assert.Empty(loaded.CommandFormatErrors);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(loaded, out var document));
        Assert.True(JToken.DeepEquals(original["focus_wrap"], document["focus_wrap"]));
        Assert.True(JToken.DeepEquals(original["cancel_command"], document["cancel_command"]));
        Assert.True(JToken.DeepEquals(original["move_command"], document["move_command"]));
    }

    [Theory]
    [InlineData("""{ "focus_wrap": "both" }""", "'focus_wrap'")]
    [InlineData("""{ "focus_wrap": "Vertical" }""", "'focus_wrap'")]
    [InlineData("""{ "focus_wrap": 1 }""", "'focus_wrap'")]
    [InlineData("""{ "focus_wrap": null }""", "'focus_wrap'")]
    [InlineData("""{ "cancel_command": 3 }""", "'cancel_command'")]
    [InlineData("""{ "move_command": true }""", "'move_command'")]
    public void AMalformedNavigationField_DoesNotThrowFromLoad_AndIsReportedWithItsPlace(string json, string location)
    {
        var asset = new UIScreenAsset();

        asset.Load(WithIdentity(json));

        Assert.Contains(asset.CommandFormatErrors, error => error.Contains(location, StringComparison.Ordinal));
        Assert.Equal(UIScreenFocusWrap.None, asset.FocusWrap);
    }

    [Fact]
    public void LoadingAgain_ForgetsThePreviousNavigationFields()
    {
        var asset = new UIScreenAsset();
        asset.Load(WithIdentity("""{ "focus_wrap": "vertical", "cancel_command": "Back", "move_command": "Tick" }"""));

        asset.Load(JObject.Parse(EnvelopeWithoutDesignTimeData));

        Assert.Equal(UIScreenFocusWrap.None, asset.FocusWrap);
        Assert.Equal(string.Empty, asset.CancelCommand);
        Assert.Equal(string.Empty, asset.MoveCommand);
    }

    private static JObject EnvelopeWith(JObject action)
    {
        var envelope = WithIdentity("{}");
        envelope["ui_commands"] = new JArray(new JObject { ["name"] = "Later", ["actions"] = new JArray(action) });
        return envelope;
    }

    // ObjectBase.Load requires both keys.
    private static JObject WithIdentity(string json)
    {
        var envelope = JObject.Parse(json);
        envelope["id"] = "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234";
        envelope["name"] = "MainMenu";
        return envelope;
    }
}
