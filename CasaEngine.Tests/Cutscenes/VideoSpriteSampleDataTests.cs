using System.Drawing;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Tests.Video;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// Guards the data of Projects/VideoSpriteSample, a project with no gameplay DLL whose startup cutscene plays a video
/// as a sprite of the world, fades another sprite in over it, and leaves on any key (ADR-0091). Running it in the
/// Launcher proves the drawing; this proves the data hangs together: the committed clip and sheet are what the
/// generators produce, every cutscene validates, and every reference resolves in the catalogue of the sample.
/// </summary>
public class VideoSpriteSampleDataTests
{
    private const string VideoEntity = "clip";
    private const string SpriteEntity = "orb";

    [Fact]
    public void TheCommittedClip_IsByteForByteTheOutputOfTheTestEncoderAndTheCopyOfTheVideoSampleClip()
    {
        var committed = File.ReadAllBytes(Path.Combine(ProjectPath(), "Videos", "clip.mdecvideo"));
        var videoSample = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "Projects", "VideoSample", "Videos", "clip.mdecvideo"));

        Assert.True(VideoSampleClip.Build().AsSpan().SequenceEqual(committed), "Videos/clip.mdecvideo differs from VideoSampleClip.Build().");
        Assert.True(videoSample.AsSpan().SequenceEqual(committed));
    }

    [Fact]
    public void TheCommittedShapesSheet_HoldsThePixelsOfTheGenerator()
    {
        using var bitmap = new Bitmap(Path.Combine(ProjectPath(), "Images", "shapes.png"));

        Assert.Equal(SpriteCutsceneSampleImages.ShapesWidth, bitmap.Width);
        Assert.Equal(SpriteCutsceneSampleImages.ShapeSize, bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var actual = bitmap.GetPixel(x, y);
                var color = x < SpriteCutsceneSampleImages.ShapeSize
                    ? SpriteCutsceneSampleImages.OrbPixel(x, y)
                    : SpriteCutsceneSampleImages.RingPixel(x - SpriteCutsceneSampleImages.ShapeSize, y);
                Assert.True(
                    actual.A == color.A && actual.R == color.R && actual.G == color.G && actual.B == color.B,
                    $"shapes.png differs at ({x}, {y}).");
            }
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
    public void TheStartupCutscene_ArmsTheExitThenLoopsAParallelOfTheVideoAndTheSpriteFade()
    {
        var root = Assert.IsType<SequenceCutsceneActionData>(LoadCutscene("loop.cutscene").RootAction);

        Assert.Equal(2, root.Actions.Count);
        var exit = Assert.IsType<ExitOnInputCutsceneActionData>(root.Actions[0]);
        var repeat = Assert.IsType<RepeatCutsceneActionData>(root.Actions[1]);
        Assert.Equal(0, repeat.Count);
        Assert.Equal(["exit"], exit.Inputs);

        var parallel = Assert.Single(repeat.Actions.OfType<ParallelCutsceneActionData>());
        var play = Assert.Single(parallel.Actions.OfType<PlayVideoSpriteCutsceneActionData>());
        Assert.Equal(VideoEntity, play.EntityName);

        var fade = Flatten(Assert.Single(parallel.Actions.OfType<SequenceCutsceneActionData>())).ToList();
        var show = Assert.Single(fade.OfType<ShowSpriteCutsceneActionData>());
        var wait = fade.OfType<WaitCutsceneActionData>().First();
        Assert.Equal(SpriteEntity, show.EntityName);
        Assert.Equal(SpriteBlendMode.Additive, show.BlendMode);
        Assert.NotEmpty(fade.OfType<TintSpriteCutsceneActionData>());
        Assert.True(wait.Seconds > 0f);
        Assert.True(fade.IndexOf(wait) < fade.IndexOf(show));

        Assert.Equal(SpriteEntity, Assert.Single(repeat.Actions.OfType<HideSpriteCutsceneActionData>()).EntityName);
    }

    [Fact]
    public void TheExitCutscene_FadesThenChangesToTheEndWorld()
    {
        var actions = Flatten(LoadCutscene("exit.cutscene").RootAction).ToList();

        var fade = Assert.Single(actions.OfType<FadeScreenCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());
        Assert.Equal("EndWorld.world", change.Target);
        Assert.True(actions.IndexOf(fade) < actions.IndexOf(change));
    }

    [Fact]
    public void TheEndCutscene_WaitsClearsTheOverlayThenChangesBackToTheFirstWorld()
    {
        var actions = Flatten(LoadCutscene("end.cutscene").RootAction).ToList();

        var wait = Assert.Single(actions.OfType<WaitCutsceneActionData>());
        var reset = Assert.Single(actions.OfType<FadeScreenCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());
        Assert.True(wait.Seconds > 0f);
        Assert.Equal(SpriteBlendMode.Subtractive, reset.BlendMode);
        Assert.Equal((0, 0, 0), (reset.R, reset.G, reset.B));
        Assert.Equal("VideoSpriteWorld.world", change.Target);
        Assert.True(actions.IndexOf(wait) < actions.IndexOf(change));
        Assert.True(actions.IndexOf(reset) < actions.IndexOf(change));
    }

    [Fact]
    public void TheExitAction_NamesTheAnyKeyEntryWithAGamePadStartAlternativeAndACataloguedCutscene()
    {
        var catalog = Catalog();
        var exit = Assert.Single(Flatten(LoadCutscene("loop.cutscene").RootAction).OfType<ExitOnInputCutsceneActionData>());

        var mappingInfo = Assert.Single(catalog, info => (string?)info["name"] == exit.Mapping);
        Assert.Equal("buttonsmapping", (string?)mappingInfo["asset_type"]);
        var mapping = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)mappingInfo["file_name"]!)));
        var button = Assert.Single(mapping["buttons"]!, entry => (string?)entry["name"] == exit.Inputs[0]);

        var input = new InputMapping();
        input.Load((JObject)button);
        Assert.Equal(InputDevices.AnyKey, input.KeyButton.InputDevice);
        Assert.Equal(InputDevices.GamePad, input.AlternativeKeyButton.InputDevice);
        Assert.Equal(Microsoft.Xna.Framework.Input.Buttons.Start, input.AlternativeKeyButton.GamePadButton);
        Assert.Equal("AnyKey", (string?)button["key_button"]!["input_device"]);

        var next = Assert.Single(catalog, info => (string?)info["name"] == exit.NextCutscene);
        Assert.Equal("cutscene", (string?)next["asset_type"]);
        Assert.Equal("Cutscenes\\exit.cutscene", (string?)next["file_name"]);
    }

    [Fact]
    public void EveryWorld_NamesACataloguedStartupCutscene()
    {
        var catalog = Catalog();

        foreach (var worldFile in new[] { "VideoSpriteWorld.world", "EndWorld.world" })
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
    public void TheWorld_HoldsTheVideoEntityAboveTheSpriteEntityBothHiddenWithoutADepthSortComponent()
    {
        var entities = EntitiesOf("VideoSpriteWorld.world");
        var video = entities[VideoEntity]["root_component"]!;
        var sprite = entities[SpriteEntity]["root_component"]!;

        Assert.Equal("Camera2dComponent", (string?)entities["camera"]["root_component"]!["type"]);
        Assert.Equal("VideoSpriteComponent", (string?)video["type"]);
        Assert.Equal("StaticSpriteComponent", (string?)sprite["type"]);
        Assert.Equal(false, (bool?)entities[VideoEntity]["is_visible"]);
        Assert.Equal(false, (bool?)entities[SpriteEntity]["is_visible"]);
        // The larger Z is drawn first, so the sprite is drawn after the video and blends over it.
        Assert.True((float)video["coordinates"]!["position"]!["z"]! > (float)sprite["coordinates"]!["position"]!["z"]!);
        Assert.DoesNotContain("DepthSortable2DComponent", File.ReadAllText(Path.Combine(ProjectPath(), "VideoSpriteWorld.world")));
    }

    [Fact]
    public void TheVideoAndTheSpriteTheDataNames_ResolveInTheCatalogue()
    {
        var catalog = Catalog();
        var entities = EntitiesOf("VideoSpriteWorld.world");

        var videoInfo = Assert.Single(catalog, info => (string?)info["name"] == (string?)entities[VideoEntity]["root_component"]!["video"]);
        Assert.Equal("mdecvideo", (string?)videoInfo["asset_type"]);

        var spriteInfo = Assert.Single(catalog, info => (string?)info["name"] == (string?)entities[SpriteEntity]["root_component"]!["spriteDataName"]);
        Assert.Equal("sprite", (string?)spriteInfo["asset_type"]);
        var sprite = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)spriteInfo["file_name"]!)));
        var sheetInfo = Assert.Single(catalog, info => (string?)info["id"] == (string?)sprite["sprite_sheet_asset_id"]);
        Assert.Equal("texture", (string?)sheetInfo["asset_type"]);
        var texture = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)sheetInfo["file_name"]!)));
        var imageInfo = Assert.Single(catalog, info => (string?)info["id"] == (string?)texture["texture_asset_id"]);
        Assert.Equal("png", (string?)imageInfo["asset_type"]);
    }

    [Fact]
    public void EveryEntityTheCutsceneNames_IsInTheWorldWithTheRightRootComponent()
    {
        var entities = EntitiesOf("VideoSpriteWorld.world");

        foreach (var action in Flatten(LoadCutscene("loop.cutscene").RootAction))
        {
            var (name, type) = action switch
            {
                PlayVideoSpriteCutsceneActionData play => (play.EntityName, "VideoSpriteComponent"),
                ShowSpriteCutsceneActionData show => (show.EntityName, "StaticSpriteComponent"),
                HideSpriteCutsceneActionData hide => (hide.EntityName, "StaticSpriteComponent"),
                TintSpriteCutsceneActionData tint => (tint.EntityName, "StaticSpriteComponent"),
                _ => (null, null),
            };

            if (name != null)
            {
                Assert.True(entities.ContainsKey(name), name);
                Assert.Equal(type, (string?)entities[name]["root_component"]!["type"]);
            }
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
    public void TheProject_HasNoDllNoFirstScreenAndStartsOnTheVideoSpriteWorld()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "VideoSpriteSample.json")));

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Equal("VideoSpriteWorld.world", (string?)project["FirstWorldLoaded"]);
    }

    private static IEnumerable<CutsceneActionData> Flatten(CutsceneActionData action)
    {
        yield return action;

        var children = action switch
        {
            SequenceCutsceneActionData sequence => sequence.Actions,
            ParallelCutsceneActionData parallel => parallel.Actions,
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

    private static string ProjectPath() => Path.Combine(RepositoryRoot(), "Projects", "VideoSpriteSample");

    private static string RepositoryRoot()
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
