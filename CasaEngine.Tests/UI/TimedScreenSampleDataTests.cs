using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Guards the data of Projects/TimedScreenSample, a project with no gameplay DLL whose worlds chain timed screens
/// from their startup cutscenes (ADR-0087). Running it in the Launcher proves the timing; this proves the data
/// hangs together: every cutscene validates, and every id and target resolves in the sample's own catalogue.
/// </summary>
public class TimedScreenSampleDataTests
{
    private static readonly string[] WorldFiles = ["FirstWorld.world", "SecondWorld.world"];

    [Theory]
    [InlineData("first.cutscene")]
    [InlineData("second.cutscene")]
    public void EveryCutscene_ValidatesWithNoError(string fileName)
    {
        var cutscene = LoadCutscene(Path.Combine(ProjectPath(), "Cutscenes", fileName));

        var result = CutsceneValidator.Validate(cutscene);

        Assert.True(result.IsValid, string.Join("; ", result.Messages.Select(message => message.Message)));
    }

    [Fact]
    public void EveryWorld_NamesACataloguedCutscene()
    {
        var catalog = Catalog();

        foreach (var worldFile in WorldFiles)
        {
            var world = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), worldFile)));
            var cutsceneId = (string?)world["startup_cutscene_asset_id"];

            Assert.False(string.IsNullOrEmpty(cutsceneId), worldFile);
            Assert.Contains(catalog, info => (string?)info["id"] == cutsceneId && (string?)info["asset_type"] == "cutscene");
        }
    }

    [Fact]
    public void EveryOpenScreenAndChangeWorldTarget_ResolvesInTheCatalogue()
    {
        var catalog = Catalog();
        var openScreens = 0;
        var changeWorlds = 0;

        foreach (var info in catalog.Where(info => (string?)info["asset_type"] == "cutscene"))
        {
            var cutscene = LoadCutscene(Path.Combine(ProjectPath(), (string)info["file_name"]!));

            foreach (var action in Flatten(cutscene.RootAction))
            {
                switch (action)
                {
                    case OpenScreenCutsceneActionData open:
                        openScreens++;
                        Assert.Contains(catalog, entry => (string?)entry["name"] == open.Target && (string?)entry["asset_type"] == "uiscreen");
                        break;
                    case ChangeWorldCutsceneActionData change:
                        changeWorlds++;
                        Assert.Contains(catalog, entry => (string?)entry["file_name"] == change.Target && (string?)entry["asset_type"] == "world");
                        break;
                }
            }
        }

        Assert.Equal(2, openScreens);
        Assert.Equal(2, changeWorlds);
    }

    [Fact]
    public void TheWorlds_ChainIntoEachOther()
    {
        var targets = new List<string?>();

        foreach (var name in new[] { "first.cutscene", "second.cutscene" })
        {
            var cutscene = LoadCutscene(Path.Combine(ProjectPath(), "Cutscenes", name));
            targets.Add(Flatten(cutscene.RootAction).OfType<ChangeWorldCutsceneActionData>().Single().Target);
        }

        Assert.Equal(["SecondWorld.world", "FirstWorld.world"], targets);
    }

    [Fact]
    public void TheProject_HasNoDllNoFirstScreenAndOneBufferedWorld()
    {
        var project = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "TimedScreenSample.json")));
        var first = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "FirstWorld.world")));
        var second = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "SecondWorld.world")));

        Assert.Equal(string.Empty, (string?)project["GameplayDllName"]);
        Assert.Equal(string.Empty, (string?)project["FirstScreenName"]);
        Assert.Equal("FirstWorld.world", (string?)project["FirstWorldLoaded"]);
        Assert.Equal("BufferedFit", (string?)first["virtual_resolution"]?["mode"]);
        Assert.Null(second["virtual_resolution"]);
    }

    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    public void EveryScreen_PassesTheChecksOfADataDrivenScreen(string screenName)
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
            AcquireFont = name =>
            {
                Assert.NotNull(Find("name", name, "fnt"));
                return new MemoryStream();
            },
        };

        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var envelopePath = Path.Combine(ProjectPath(), "Screens", screenName + ".uiscreen");
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse(File.ReadAllText(envelopePath)));
        var screen = new DataDrivenScreen(asset, envelopePath, context);

        Assert.NotNull(screen.BuildWindow(desktop));
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

    private static CutsceneAsset LoadCutscene(string path)
    {
        var cutscene = new CutsceneAsset();
        cutscene.Load(JObject.Parse(File.ReadAllText(path)));
        return cutscene;
    }

    private static JToken Catalog()
        => JObject.Parse(File.ReadAllText(Path.Combine(ProjectPath(), "AssetInfos.json")))["asset_infos"]!;

    private static string ProjectPath() => Path.Combine(FindRepositoryRoot(), "Projects", "TimedScreenSample");

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
