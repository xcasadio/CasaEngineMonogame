using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Gameplay;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// ADR-0077 (plan world-game-mode-inspector T1): the editor writes a <c>.gameMode</c> asset
/// (<see cref="PlayerStartupSettings"/>) with the keys <see cref="PlayerStartupSettings.Load"/> reads, under their current
/// names, so a written file reads back identically.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class PlayerStartupSettingsWriterTests
{
    private const string RelativePath = "RpgGameMode.gameMode";

    // Projects/RPGDemo/RpgGameMode.gameMode as committed: it still carries the legacy "hud_classClass" key.
    private const string LegacyRpgGameMode = """
        {
          "id": "42454808-0e3a-4c44-98a6-e3485e7ec9fb",
          "name": "Object 42454808-0e3a-4c44-98a6-e3485e7ec9fb",
          "default_pawn_asset_id": "faab700a-eb3c-4192-8ac9-d5907af17780",
          "player_controller_class": "",
          "hud_classClass": null
        }
        """;

    [Fact]
    public void SaveAsset_WritesTheGameModeFile_AndRaisesAssetSavedFromTheWorldInspector()
    {
        var settings = LoadSettings(CompleteDocument());

        WithProject((projectPath, save) =>
        {
            EditorAssetSavedEventArgs? raisedEvent = null;
            void OnAssetSaved(object? sender, EditorAssetSavedEventArgs e) => raisedEvent = e;

            EditorAssetWriterService.AssetSaved += OnAssetSaved;
            try
            {
                save(settings);
            }
            finally
            {
                EditorAssetWriterService.AssetSaved -= OnAssetSaved;
            }

            Assert.True(File.Exists(Path.Combine(projectPath, RelativePath)));
            Assert.NotNull(raisedEvent);
            Assert.Equal(RelativePath, raisedEvent!.RelativePath);
            Assert.Equal(settings.Id, raisedEvent.AssetId);
            Assert.Equal(EditorAssetSaveSource.WorldInspector, raisedEvent.SaveSource);
        });
    }

    [Fact]
    public void SaveAsset_ThenLoad_GivesBackEveryField()
    {
        var settings = LoadSettings(CompleteDocument());

        WithProject((projectPath, save) =>
        {
            save(settings);

            var reloaded = LoadSettings(ReadDocument(projectPath));
            AssertSameSettings(settings, reloaded);
            Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), reloaded.Id);
            Assert.Equal("Adventure", reloaded.Name);
            Assert.Equal(Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"), reloaded.DefaultPawnAssetId);
            Assert.Equal("Game.Scripts.HeroController", reloaded.PlayerControllerClass);
            Assert.Equal("Game.Scripts.EnemyController", reloaded.AIControllerClass);
            Assert.Equal("Game.Scripts.Hud", reloaded.HUDClass);
        });
    }

    [Fact]
    public void SaveAsset_ThenLoad_KeepsAnEmptyControllerAndANullHud()
    {
        var document = CompleteDocument();
        document["player_controller_class"] = string.Empty;
        document["hud_class"] = null;
        var settings = LoadSettings(document);

        WithProject((projectPath, save) =>
        {
            save(settings);

            var reloaded = LoadSettings(ReadDocument(projectPath));
            AssertSameSettings(settings, reloaded);
            Assert.Equal(string.Empty, reloaded.PlayerControllerClass);
            Assert.Null(reloaded.HUDClass);
        });
    }

    [Fact]
    public void SaveAsset_OfTheLegacyRpgGameMode_WritesHudClassAndReadsBackTheSame()
    {
        var settings = LoadSettings(JObject.Parse(LegacyRpgGameMode));

        WithProject((projectPath, save) =>
        {
            save(settings);

            var written = ReadDocument(projectPath);
            Assert.True(written.ContainsKey("hud_class"));
            Assert.False(written.ContainsKey("hud_classClass"));
            Assert.Equal(JTokenType.Null, written["hud_class"]!.Type);

            var reloaded = LoadSettings(written);
            AssertSameSettings(settings, reloaded);
            Assert.Equal(Guid.Parse("faab700a-eb3c-4192-8ac9-d5907af17780"), reloaded.DefaultPawnAssetId);
        });
    }

    [Fact]
    public void SaveAsset_WritesExactlyTheKeysLoadReads()
    {
        var settings = LoadSettings(CompleteDocument());

        WithProject((projectPath, save) =>
        {
            save(settings);

            var keys = ReadDocument(projectPath).Properties().Select(property => property.Name).ToArray();
            Assert.Equal(
                new[] { "id", "name", "default_pawn_asset_id", "player_controller_class", "ai_controller_class", "hud_class" },
                keys);
        });
    }

    private static JObject CompleteDocument() => new()
    {
        ["id"] = "11111111-2222-3333-4444-555555555555",
        ["name"] = "Adventure",
        ["default_pawn_asset_id"] = "66666666-7777-8888-9999-aaaaaaaaaaaa",
        ["player_controller_class"] = "Game.Scripts.HeroController",
        ["ai_controller_class"] = "Game.Scripts.EnemyController",
        ["hud_class"] = "Game.Scripts.Hud",
    };

    private static PlayerStartupSettings LoadSettings(JObject document)
    {
        var settings = new PlayerStartupSettings();
        settings.Load(document);
        return settings;
    }

    private static JObject ReadDocument(string projectPath) =>
        JObject.Parse(File.ReadAllText(Path.Combine(projectPath, RelativePath)));

    private static void AssertSameSettings(PlayerStartupSettings expected, PlayerStartupSettings actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DefaultPawnAssetId, actual.DefaultPawnAssetId);
        Assert.Equal(expected.PlayerControllerClass, actual.PlayerControllerClass);
        Assert.Equal(expected.AIControllerClass, actual.AIControllerClass);
        Assert.Equal(expected.HUDClass, actual.HUDClass);
    }

    private static void WithProject(Action<string, Action<PlayerStartupSettings>> body)
    {
        string projectPath = Path.Combine(Path.GetTempPath(), "casa-gamemode-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectPath);
        string? previousProjectPath = EngineEnvironment.ProjectPath;

        try
        {
            EngineEnvironment.ProjectPath = projectPath;
            body(projectPath, settings => EditorAssetWriterService.SaveAsset(RelativePath, settings, EditorAssetSaveSource.WorldInspector));
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(projectPath, recursive: true);
        }
    }
}
