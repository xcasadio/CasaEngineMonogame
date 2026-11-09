using System.Drawing;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Rendering.Depth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// Guards the data of Projects/SpriteCutsceneSample, a project with no gameplay DLL whose startup cutscene drives four
/// sprite entities and leaves on a named input (ADR-0090). Running it in the Launcher proves the drawing; this proves the
/// data hangs together: the committed images hold the pixels the generator produces, every cutscene validates, and every
/// world, cutscene, mapping, entity and sprite reference resolves in the sample's own catalogue.
/// </summary>
public class SpriteCutsceneSampleDataTests
{
    private static readonly string[] SpriteEntities = ["orb-left", "orb-right", "ring-left", "ring-right"];

    [Fact]
    public void TheCommittedShapesSheet_HoldsThePixelsOfTheGenerator()
    {
        AssertPixels("shapes.png", SpriteCutsceneSampleImages.ShapesWidth, SpriteCutsceneSampleImages.ShapeSize,
            (x, y) => x < SpriteCutsceneSampleImages.ShapeSize
                ? SpriteCutsceneSampleImages.OrbPixel(x, y)
                : SpriteCutsceneSampleImages.RingPixel(x - SpriteCutsceneSampleImages.ShapeSize, y));
    }

    [Fact]
    public void TheCommittedBackdrop_HoldsThePixelsOfTheGenerator()
    {
        AssertPixels("backdrop.png", SpriteCutsceneSampleImages.BackdropWidth, SpriteCutsceneSampleImages.BackdropHeight, SpriteCutsceneSampleImages.BackdropPixel);
    }

    [Fact]
    public void TheGeneratedPng_DecodesWithTheExpectedSizeAndTransparency()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sprite-cutscene-sample-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, SpriteCutsceneSampleImages.BuildShapesPng());
            using var bitmap = new Bitmap(path);

            Assert.Equal(SpriteCutsceneSampleImages.ShapesWidth, bitmap.Width);
            Assert.Equal(SpriteCutsceneSampleImages.ShapeSize, bitmap.Height);
            Assert.Equal(0, bitmap.GetPixel(0, 0).A);
            Assert.Equal(255, bitmap.GetPixel(SpriteCutsceneSampleImages.ShapeSize / 2, SpriteCutsceneSampleImages.ShapeSize / 2).A);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("loop.cutscene")]
    [InlineData("exit.cutscene")]
    [InlineData("end.cutscene")]
    public void EveryCutscene_ValidatesWithNoError(string file)
    {
        var result = CutsceneValidator.Validate(LoadCutscene(file));

        Assert.True(result.IsValid, string.Join("; ", result.Messages.Select(message => message.Message)));
    }

    [Fact]
    public void TheStartupCutscene_ArmsTheExitThenLoopsForEver()
    {
        var root = Assert.IsType<SequenceCutsceneActionData>(LoadCutscene("loop.cutscene").RootAction);

        Assert.Equal(2, root.Actions.Count);
        var exit = Assert.IsType<ExitOnInputCutsceneActionData>(root.Actions[0]);
        var repeat = Assert.IsType<RepeatCutsceneActionData>(root.Actions[1]);
        Assert.Equal(0, repeat.Count);
        Assert.Equal(["exit"], exit.Inputs);

        var actions = repeat.Actions;
        Assert.Contains(actions.OfType<ShowSpriteCutsceneActionData>(), action => action.BlendMode == SpriteBlendMode.Additive);
        Assert.Contains(actions.OfType<ShowSpriteCutsceneActionData>(), action => action.BlendMode == SpriteBlendMode.Subtractive);
        Assert.NotEmpty(actions.OfType<MoveSpriteCutsceneActionData>());
        Assert.NotEmpty(actions.OfType<TintSpriteCutsceneActionData>());
        Assert.NotEmpty(actions.OfType<WaitCutsceneActionData>());
        Assert.Equal(SpriteEntities.Order(), actions.OfType<HideSpriteCutsceneActionData>().Select(action => action.EntityName!).Order());
    }

    [Fact]
    public void TheExitCutscene_FadesToBlackThenChangesToTheEndWorld()
    {
        var actions = Flatten(LoadCutscene("exit.cutscene").RootAction).ToList();

        var fade = Assert.Single(actions.OfType<FadeScreenCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());
        Assert.Equal(SpriteBlendMode.Subtractive, fade.BlendMode);
        Assert.Equal((255, 255, 255), (fade.R, fade.G, fade.B));
        Assert.Equal("EndWorld.world", change.Target);
        Assert.True(actions.IndexOf(fade) < actions.IndexOf(change));
    }

    [Fact]
    public void TheEndCutscene_WaitsThenChangesBackToTheFirstWorld()
    {
        var actions = Flatten(LoadCutscene("end.cutscene").RootAction).ToList();

        var wait = Assert.Single(actions.OfType<WaitCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());
        Assert.True(wait.Seconds > 0f);
        Assert.Equal("SpriteWorld.world", change.Target);
        Assert.True(actions.IndexOf(wait) < actions.IndexOf(change));
    }

    [Fact]
    public void TheExitAction_NamesACataloguedMappingEntryAndACataloguedCutscene()
    {
        var catalog = Catalog();
        var exit = Assert.Single(Flatten(LoadCutscene("loop.cutscene").RootAction).OfType<ExitOnInputCutsceneActionData>());

        var mappingInfo = Assert.Single(catalog, info => (string?)info["name"] == exit.Mapping);
        Assert.Equal("buttonsmapping", (string?)mappingInfo["asset_type"]);
        var mapping = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)mappingInfo["file_name"]!)));
        var button = Assert.Single(mapping["buttons"]!, entry => (string?)entry["name"] == exit.Inputs[0]);
        Assert.NotEqual("None", (string?)button["key_button"]!["key"]);
        Assert.Equal("Keyboard", (string?)button["key_button"]!["input_device"]);
        Assert.NotEqual("None", (string?)button["alternative_key_button"]!["game_pad_button"]);
        Assert.Equal("GamePad", (string?)button["alternative_key_button"]!["input_device"]);

        var next = Assert.Single(catalog, info => (string?)info["name"] == exit.NextCutscene);
        Assert.Equal("cutscene", (string?)next["asset_type"]);
        Assert.Equal("Cutscenes\\exit.cutscene", (string?)next["file_name"]);
    }

    [Fact]
    public void EveryWorld_NamesACataloguedStartupCutscene()
    {
        var catalog = Catalog();

        foreach (var worldFile in new[] { "SpriteWorld.world", "EndWorld.world" })
        {
            var cutsceneId = (string?)LoadWorld(worldFile)["startup_cutscene_asset_id"];

            Assert.False(string.IsNullOrEmpty(cutsceneId), worldFile);
            Assert.Contains(catalog, info => (string?)info["id"] == cutsceneId && (string?)info["asset_type"] == "cutscene");
        }
    }

    [Fact]
    public void EveryChangeWorldTarget_IsACataloguedWorld()
    {
        var catalog = Catalog();

        foreach (var file in new[] { "loop.cutscene", "exit.cutscene", "end.cutscene" })
        {
            foreach (var change in Flatten(LoadCutscene(file).RootAction).OfType<ChangeWorldCutsceneActionData>())
            {
                Assert.Contains(catalog, info => (string?)info["file_name"] == change.Target && (string?)info["asset_type"] == "world");
            }
        }
    }

    [Fact]
    public void TheSpriteWorld_HoldsTheFourEntitiesHiddenAndWithoutADepthSortComponent()
    {
        var entities = EntitiesOf("SpriteWorld.world");

        foreach (var name in SpriteEntities)
        {
            var entity = entities[name];
            Assert.Equal(false, (bool?)entity["is_visible"]);
            Assert.Equal("StaticSpriteComponent", (string?)entity["root_component"]!["type"]);
            Assert.Empty(entity["components"]!);
        }

        Assert.Equal("Camera2dComponent", (string?)entities["camera"]["root_component"]!["type"]);
        Assert.Null(entities["backdrop"]["is_visible"]);
        Assert.Equal("StaticSpriteComponent", (string?)entities["backdrop"]["root_component"]!["type"]);
        Assert.DoesNotContain("DepthSortable2DComponent", File.ReadAllText(Path.Combine(ProjectPath(), "SpriteWorld.world")));
    }

    [Fact]
    public void EveryEntityTheCutsceneNames_IsAStaticSpriteOfTheSpriteWorld()
    {
        var entities = EntitiesOf("SpriteWorld.world");

        foreach (var action in Flatten(LoadCutscene("loop.cutscene").RootAction))
        {
            var name = action switch
            {
                ShowSpriteCutsceneActionData show => show.EntityName,
                HideSpriteCutsceneActionData hide => hide.EntityName,
                MoveSpriteCutsceneActionData move => move.EntityName,
                TintSpriteCutsceneActionData tint => tint.EntityName,
                _ => null,
            };

            if (name != null)
            {
                Assert.True(entities.ContainsKey(name), name);
                Assert.Equal("StaticSpriteComponent", (string?)entities[name]["root_component"]!["type"]);
            }
        }
    }

    [Fact]
    public void EverySpriteTheDataNames_ResolvesToACataloguedSpriteOnACataloguedImage()
    {
        var catalog = Catalog();
        var names = new HashSet<string>();
        foreach (var entity in EntitiesOf("SpriteWorld.world").Values)
        {
            var name = (string?)entity["root_component"]!["spriteDataName"];
            if (name != null && name != "null")
            {
                names.Add(name);
            }
        }

        foreach (var show in Flatten(LoadCutscene("loop.cutscene").RootAction).OfType<ShowSpriteCutsceneActionData>())
        {
            if (show.Sprite != null)
            {
                names.Add(show.Sprite);
            }
        }

        Assert.Equal(["backdrop-sprite", "orb", "ring"], names.Order());

        foreach (var name in names)
        {
            var spriteInfo = Assert.Single(catalog, info => (string?)info["name"] == name);
            Assert.Equal("sprite", (string?)spriteInfo["asset_type"]);
            var sprite = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)spriteInfo["file_name"]!)));
            var sheetInfo = Assert.Single(catalog, info => (string?)info["id"] == (string?)sprite["sprite_sheet_asset_id"]);
            Assert.Equal("texture", (string?)sheetInfo["asset_type"]);
            var texture = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)sheetInfo["file_name"]!)));
            var imageInfo = Assert.Single(catalog, info => (string?)info["id"] == (string?)texture["texture_asset_id"]);
            Assert.Equal("png", (string?)imageInfo["asset_type"]);

            using var bitmap = new Bitmap(Path.Combine(ProjectPath(), (string)imageInfo["file_name"]!));
            var location = sprite["location"]!;
            Assert.True((int)location["x"]! + (int)location["w"]! <= bitmap.Width, name);
            Assert.True((int)location["y"]! + (int)location["h"]! <= bitmap.Height, name);
        }
    }

    [Fact]
    public void EveryCataloguedFile_ExistsAndEveryIdAndNameIsUnique()
    {
        var catalog = Catalog();

        foreach (var info in catalog)
        {
            Assert.True(File.Exists(Path.Combine(ProjectPath(), (string)info["file_name"]!)), (string?)info["file_name"]);
        }

        Assert.Equal(catalog.Count(), catalog.Select(info => (string?)info["id"]).Distinct().Count());
        Assert.Equal(catalog.Count(), catalog.Select(info => (string?)info["name"]).Distinct().Count());
    }

    [Fact]
    public void TheProject_HasNoDllNoFirstScreenAndStartsOnTheSpriteWorld()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "SpriteCutsceneSample.json")));

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Equal("SpriteWorld.world", (string?)project["FirstWorldLoaded"]);
    }

    private static void AssertPixels(string file, int width, int height, Func<int, int, Microsoft.Xna.Framework.Color> expected)
    {
        using var bitmap = new Bitmap(Path.Combine(ProjectPath(), "Images", file));

        Assert.Equal(width, bitmap.Width);
        Assert.Equal(height, bitmap.Height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var actual = bitmap.GetPixel(x, y);
                var color = expected(x, y);
                Assert.True(
                    actual.A == color.A && actual.R == color.R && actual.G == color.G && actual.B == color.B,
                    $"{file} differs at ({x}, {y}): committed ({actual.R},{actual.G},{actual.B},{actual.A}), generated ({color.R},{color.G},{color.B},{color.A}).");
            }
        }
    }

    private static IEnumerable<CutsceneActionData> Flatten(CutsceneActionData action)
    {
        yield return action;

        var children = action switch
        {
            SequenceCutsceneActionData sequence => sequence.Actions,
            RepeatCutsceneActionData repeat => repeat.Actions,
            _ => null,
        };

        if (children == null)
        {
            yield break;
        }

        foreach (var child in children)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static CutsceneAsset LoadCutscene(string file)
    {
        var cutscene = new CutsceneAsset();
        cutscene.Load(JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Cutscenes", file))));
        return cutscene;
    }

    private static JObject LoadWorld(string file) => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), file)));

    private static Dictionary<string, JToken> EntitiesOf(string worldFile)
        => LoadWorld(worldFile)["entity_references"]!.ToDictionary(reference => (string)reference["entity"]!["name"]!, reference => reference["entity"]!);

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", "SpriteCutsceneSample");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}
