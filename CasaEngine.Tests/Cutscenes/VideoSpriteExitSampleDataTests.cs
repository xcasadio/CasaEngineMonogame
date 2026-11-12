using CasaEngine.Engine.Input;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Video;
using CasaEngine.Tests.Video;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// Guards the data of Projects/VideoSpriteExitSample, a project with no gameplay DLL whose startup cutscene starts a
/// world video with StartVideoSprite at each lap and leaves on any key (ADR-0093): after the key, the exit cutscene dims
/// the screen while the film runs to its end. Running it in the Launcher proves the drawing; this proves the data hangs
/// together: the committed clip is what the generator produces, every cutscene validates, the waits outlast the film,
/// and every reference resolves in the catalogue of the sample.
/// </summary>
public class VideoSpriteExitSampleDataTests
{
    private const string VideoEntity = "clip";
    private const string FilmWorld = "ExitFilmWorld.world";

    [Fact]
    public void TheCommittedClip_IsByteForByteTheOutputOfTheTestEncoderAndTheCopyOfTheVideoSpriteSampleClip()
    {
        var committed = File.ReadAllBytes(Path.Combine(ProjectPath(), "Videos", "clip.mdecvideo"));
        var neighbour = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "Projects", "VideoSpriteSample", "Videos", "clip.mdecvideo"));

        Assert.True(VideoSampleClip.Build().AsSpan().SequenceEqual(committed), "Videos/clip.mdecvideo differs from VideoSampleClip.Build().");
        Assert.True(neighbour.AsSpan().SequenceEqual(committed));
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
    public void TheStartupCutscene_ArmsTheExitThenStartsTheFilmAtEachLap_WithAWaitLongerThanTheFilm()
    {
        var root = Assert.IsType<SequenceCutsceneActionData>(LoadCutscene("loop.cutscene").RootAction);

        Assert.Equal(2, root.Actions.Count);
        var exit = Assert.IsType<ExitOnInputCutsceneActionData>(root.Actions[0]);
        var repeat = Assert.IsType<RepeatCutsceneActionData>(root.Actions[1]);
        Assert.Equal(["exit"], exit.Inputs);
        Assert.Equal(0, repeat.Count);
        Assert.Equal(2, repeat.Actions.Count);

        var start = Assert.IsType<StartVideoSpriteCutsceneActionData>(repeat.Actions[0]);
        var wait = Assert.IsType<WaitCutsceneActionData>(repeat.Actions[1]);
        Assert.Equal(VideoEntity, start.EntityName);
        Assert.True(wait.Seconds > ClipSeconds(), "each lap must outlast the film, so the entity is hidden between laps");
        Assert.Empty(Flatten(root).OfType<PlayVideoSpriteCutsceneActionData>());
    }

    [Fact]
    public void TheExitCutscene_DimsTheScreenWithoutBlackingIt_WaitsLongerThanTheFilm_ThenChangesToTheEndWorld()
    {
        var actions = Flatten(LoadCutscene("exit.cutscene").RootAction).ToList();

        var fade = Assert.Single(actions.OfType<FadeScreenCutsceneActionData>());
        var wait = Assert.Single(actions.OfType<WaitCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());
        Assert.Equal(SpriteBlendMode.Subtractive, fade.BlendMode);
        Assert.Equal((128, 128, 128), (fade.R, fade.G, fade.B));
        Assert.True(fade.DurationSeconds + wait.Seconds > ClipSeconds(), "the exit must outlast the rest of the film");
        Assert.Equal("EndWorld.world", change.Target);
        Assert.True(actions.IndexOf(fade) < actions.IndexOf(wait));
        Assert.True(actions.IndexOf(wait) < actions.IndexOf(change));
    }

    [Fact]
    public void TheEndCutscene_WaitsClearsTheOverlayThenChangesBackToTheFilmWorld()
    {
        var actions = Flatten(LoadCutscene("end.cutscene").RootAction).ToList();

        var wait = Assert.Single(actions.OfType<WaitCutsceneActionData>());
        var reset = Assert.Single(actions.OfType<FadeScreenCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());
        Assert.True(wait.Seconds > 0f);
        Assert.Equal(SpriteBlendMode.Subtractive, reset.BlendMode);
        Assert.Equal((0, 0, 0), (reset.R, reset.G, reset.B));
        Assert.Equal(FilmWorld, change.Target);
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

        var next = Assert.Single(catalog, info => (string?)info["name"] == exit.NextCutscene);
        Assert.Equal("cutscene", (string?)next["asset_type"]);
        Assert.Equal("Cutscenes\\exit.cutscene", (string?)next["file_name"]);
    }

    [Fact]
    public void EveryWorld_NamesACataloguedStartupCutscene()
    {
        var catalog = Catalog();

        foreach (var worldFile in new[] { FilmWorld, "EndWorld.world" })
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
    public void TheFilmWorld_HoldsACameraAndTheHiddenVideoEntity_WhoseVideoIsCatalogued()
    {
        var catalog = Catalog();
        var entities = EntitiesOf(FilmWorld);

        Assert.Equal(2, entities.Count);
        Assert.Equal("Camera2dComponent", (string?)entities["camera"]["root_component"]!["type"]);
        var video = entities[VideoEntity]["root_component"]!;
        Assert.Equal("VideoSpriteComponent", (string?)video["type"]);
        Assert.Equal(false, (bool?)entities[VideoEntity]["is_visible"]);

        var videoInfo = Assert.Single(catalog, info => (string?)info["name"] == (string?)video["video"]);
        Assert.Equal("mdecvideo", (string?)videoInfo["asset_type"]);
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
    public void TheProject_HasNoDllNoFirstScreenAndStartsOnTheFilmWorld()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "VideoSpriteExitSample.json")));

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Equal(FilmWorld, (string?)project["FirstWorldLoaded"]);
    }

    /// <summary>The length of the committed clip, read from its container.</summary>
    private static double ClipSeconds()
    {
        using var stream = File.OpenRead(Path.Combine(ProjectPath(), "Videos", "clip.mdecvideo"));
        var asset = MdecVideoAsset.Read(stream, "clip.mdecvideo");
        return asset.FrameCount * (double)asset.FrameRateDenominator / asset.FrameRateNumerator;
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

    private static string ProjectPath() => Path.Combine(RepositoryRoot(), "Projects", "VideoSpriteExitSample");

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
