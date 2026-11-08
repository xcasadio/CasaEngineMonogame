using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Framework.Video;
using CasaEngine.Tests.Video;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Guards the data of Projects/VideoSample, a project with no gameplay DLL whose startup cutscene plays a synthetic MDEC
/// clip on a screen and then loops (ADR-0089). Running it in the Launcher proves the playback; this proves the data
/// hangs together: the committed clip is byte for byte what the test encoder produces, the cutscene validates, and every
/// id, target and source name resolves in the sample's own catalogue.
/// </summary>
public class VideoSampleDataTests
{
    [Fact]
    public void TheCommittedClip_IsByteForByteTheOutputOfTheTestEncoder()
    {
        var committed = File.ReadAllBytes(Path.Combine(ProjectPath(), "Videos", "clip.mdecvideo"));

        Assert.True(VideoSampleClip.Build().AsSpan().SequenceEqual(committed), "Videos/clip.mdecvideo differs from VideoSampleClip.Build().");
    }

    [Fact]
    public void TheCommittedClip_LoadsWithTheDeclaredHeader()
    {
        using var stream = File.OpenRead(Path.Combine(ProjectPath(), "Videos", "clip.mdecvideo"));

        var asset = MdecVideoAsset.Read(stream, "clip.mdecvideo");

        Assert.Equal(VideoSampleClip.Width, asset.Width);
        Assert.Equal(VideoSampleClip.Height, asset.Height);
        Assert.Equal(VideoSampleClip.FrameCount, asset.FrameCount);
        Assert.Equal(VideoSampleClip.FramesPerSecond, asset.FrameRateNumerator);
        Assert.Equal(1u, asset.FrameRateDenominator);
        Assert.False(asset.HasAudio);
    }

    [Fact]
    public void TheCutscene_ValidatesWithNoError()
    {
        var cutscene = LoadCutscene();

        var result = CutsceneValidator.Validate(cutscene);

        Assert.True(result.IsValid, string.Join("; ", result.Messages.Select(message => message.Message)));
    }

    [Fact]
    public void TheCutscene_PlaysTheClipScreenThenReloadsTheWorld()
    {
        var catalog = Catalog();
        var actions = Flatten(LoadCutscene().RootAction).ToList();

        var play = Assert.Single(actions.OfType<PlayVideoCutsceneActionData>());
        var change = Assert.Single(actions.OfType<ChangeWorldCutsceneActionData>());

        Assert.Contains(catalog, entry => (string?)entry["name"] == play.Target && (string?)entry["asset_type"] == "uiscreen");
        Assert.Contains(catalog, entry => (string?)entry["file_name"] == change.Target && (string?)entry["asset_type"] == "world");
        Assert.Equal("VideoWorld.world", change.Target);
        Assert.True(actions.IndexOf(play) < actions.IndexOf(change));
    }

    [Fact]
    public void TheWorld_NamesTheCataloguedCutscene()
    {
        var world = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "VideoWorld.world")));
        var cutsceneId = (string?)world["startup_cutscene_asset_id"];

        Assert.False(string.IsNullOrEmpty(cutsceneId));
        Assert.Contains(Catalog(), info => (string?)info["id"] == cutsceneId && (string?)info["asset_type"] == "cutscene");
    }

    [Fact]
    public void TheProject_HasNoDllNoFirstScreenAndACataloguedUiInputMapping()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "VideoSample.json")));
        var mappingName = (string?)project["UIInputMapping"];

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Equal("VideoWorld.world", (string?)project["FirstWorldLoaded"]);
        Assert.False(string.IsNullOrEmpty(mappingName));

        var info = Assert.Single(Catalog(), entry => (string?)entry["name"] == mappingName);
        Assert.Equal("buttonsmapping", (string?)info["asset_type"]);

        var mapping = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), (string)info["file_name"]!)));
        Assert.Contains(mapping["buttons"]!, button => (string?)button["name"] == "Submit");
    }

    [Fact]
    public void TheClipScreen_HoldsOneSkippableVideoOfACataloguedClip()
    {
        var catalog = Catalog();
        var xaml = File.ReadAllText(Path.Combine(ProjectPath(), "Screens", "clip.xaml"));
        var envelope = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Screens", "clip.uiscreen")));

        Assert.Equal("clip.xaml", (string?)envelope["source_xaml_file"]);
        Assert.Contains("IsSkippable=\"True\"", xaml);
        Assert.Contains("SourceName=\"clip\"", xaml);
        Assert.Contains(catalog, info => (string?)info["name"] == "clip" && (string?)info["asset_type"] == "mdecvideo");
        Assert.Contains(catalog, info => (string?)info["file_name"] == "Videos\\clip.mdecvideo");
    }

    [Fact]
    public void TheClipScreen_PassesTheChecksOfADataDrivenScreen()
    {
        var catalog = Catalog();
        JToken? Find(string key, string value, string assetType)
            => catalog.FirstOrDefault(info => (string?)info[key] == value && (string?)info["asset_type"] == assetType);

        var context = new UIScreenActionContext
        {
            ScreenExists = name => Find("name", name, "uiscreen") != null,
            WorldExists = worldFile => Find("file_name", worldFile, "world") != null,
            AcquireSound = static _ => throw new InvalidOperationException("The sample has no sound."),
            IsTopScreen = static _ => true,
            OpenScreen = static _ => { },
            CloseScreen = static _ => { },
            ChangeWorld = static _ => { },
            PlaySound = static _ => { },
            Quit = static () => { },
            StartFade = static _ => { },
            IsFadeRunning = static () => false,
            AcquireFont = static _ => throw new InvalidOperationException("The sample has no font."),
            HasUIInputMapping = true,
        };

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(ProjectPath(), "Screens", "clip.uiscreen");
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(File.ReadAllText(envelopePath)));
        var screen = new DataDrivenScreen(asset, envelopePath, context);

        Assert.NotNull(screen.BuildWindow(desktop));
        Assert.Equal(1, screen.VideoCount);
        screen.Dispose();
    }

    private static IEnumerable<CutsceneActionData> Flatten(CutsceneActionData action)
    {
        yield return action;

        if (action is SequenceCutsceneActionData sequence)
        {
            foreach (var child in sequence.Actions)
            {
                foreach (var nested in Flatten(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static CutsceneAsset LoadCutscene()
    {
        var cutscene = new CutsceneAsset();
        cutscene.Load(JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "Cutscenes", "play.cutscene"))));
        return cutscene;
    }

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", "VideoSample");

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
