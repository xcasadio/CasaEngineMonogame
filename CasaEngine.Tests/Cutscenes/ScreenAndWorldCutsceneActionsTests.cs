using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Cutscenes.Serialization;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting.Coroutines;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The OpenScreen and ChangeWorld cutscene actions: JSON, validation, the GameManager method that opens a screen,
/// and the action factory. The catalogue is static, so this class runs in <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class ScreenAndWorldCutsceneActionsTests : IDisposable
{
    private static readonly Guid ScreenId = Guid.Parse("11111111-aaaa-4aaa-8aaa-111111111111");
    private static readonly Guid WorldId = Guid.Parse("22222222-aaaa-4aaa-8aaa-222222222222");

    private readonly GameManager _gameManager = new(null);
    private readonly List<string> _created = new();

    public ScreenAndWorldCutsceneActionsTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ScreenId) { Name = "Intro", FileName = "intro.uiscreen", AssetType = "uiscreen" });
        AssetCatalog.AddInternal(new AssetInfo(WorldId) { Name = "Second", FileName = "second.world", AssetType = "world" });
    }

    public void Dispose()
    {
        Logs.Close();
        AssetCatalog.ClearInternal();
    }

    // ---- JSON and validation ----

    [Fact]
    public void SaveLoad_RoundTripsBothActions()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions =
                {
                    new OpenScreenCutsceneActionData { Target = "Intro" },
                    new ChangeWorldCutsceneActionData { Target = "second.world" },
                },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        Assert.Equal("Intro", Assert.IsType<OpenScreenCutsceneActionData>(root.Actions[0]).Target);
        Assert.Equal("second.world", Assert.IsType<ChangeWorldCutsceneActionData>(root.Actions[1]).Target);
        Assert.True(loaded.Validate().IsValid);
        Assert.Equal("OpenScreen", node["root_action"]!["actions"]![0]!["type"]!.ToString());
        Assert.Equal("ChangeWorld", node["root_action"]!["actions"]![1]!["type"]!.ToString());
    }

    [Theory]
    [InlineData("OpenScreen", "{\"type\":\"OpenScreen\"}")]
    [InlineData("OpenScreen", "{\"type\":\"OpenScreen\",\"target\":\"\"}")]
    [InlineData("OpenScreen", "{\"type\":\"OpenScreen\",\"target\":\"  \"}")]
    [InlineData("OpenScreen", "{\"type\":\"OpenScreen\",\"target\":3}")]
    [InlineData("ChangeWorld", "{\"type\":\"ChangeWorld\"}")]
    [InlineData("ChangeWorld", "{\"type\":\"ChangeWorld\",\"target\":\"\"}")]
    public void ATargetThatIsAbsentOrEmpty_IsAValidatorErrorNamingTheActionAndTheField(string action, string json)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = JObject.Parse(json) };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);

        var messages = asset.Validate().Messages;

        var error = Assert.Single(messages, message => message.Severity == CutsceneValidationSeverity.Error);
        Assert.Contains($"{action}.target", error.Message);
    }

    // ---- GameManager.OpenScreenFromCutscene ----

    [Fact]
    public void InTheRuntime_TheScreenIsPushedOnTheActiveView()
    {
        var view = AddView();

        string failure = _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Intro", Create);

        Assert.Null(failure);
        Assert.Equal(new[] { "Intro" }, _created);
        Assert.Single(view.Pushed);
    }

    [Fact]
    public void TheScreenCanBeDesignatedByItsId()
    {
        var view = AddView();

        string failure = _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, ScreenId.ToString(), Create);

        Assert.Null(failure);
        Assert.Single(view.Pushed);
    }

    [Fact]
    public void OpeningTwice_PushesTwice()
    {
        var view = AddView();

        _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Intro", Create);
        _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Intro", Create);

        Assert.Equal(2, view.Pushed.Count);
    }

    public static TheoryData<string> EditorPolicies() => new() { "preview", "simulation" };

    private static GameplayExecutionPolicy EditorPolicy(string name)
        => name == "preview" ? GameplayExecutionPolicies.EditorPreview : GameplayExecutionPolicies.EditorSimulation;

    [Theory]
    [MemberData(nameof(EditorPolicies))]
    public void InTheEditor_NothingIsOpened_AndNoFailureIsReturned(string policy)
    {
        var view = AddView();

        string failure = _gameManager.OpenScreenFromCutscene(EditorPolicy(policy), "Intro", Create);

        Assert.Null(failure);
        Assert.Empty(_created);
        Assert.Empty(view.Pushed);
    }

    [Fact]
    public void WithoutAnActiveView_TheReasonIsReturned()
    {
        string failure = _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Intro", Create);

        Assert.NotNull(failure);
        Assert.Contains("Intro", failure);
        Assert.Empty(_created);
    }

    [Fact]
    public void WithAViewThatHostsNoUI_TheReasonIsReturned()
    {
        AddViewWithRuntime(null);

        string failure = _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Intro", Create);

        Assert.NotNull(failure);
    }

    [Fact]
    public void AScreenAbsentFromTheCatalogue_TheReasonIsReturned_AndNothingIsCreated()
    {
        AddView();

        string failure = _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Missing", Create);

        Assert.NotNull(failure);
        Assert.Contains("Missing", failure);
        Assert.Empty(_created);
    }

    [Fact]
    public void AnAssetThatIsNotAScreen_IsNotOpened()
    {
        AddView();

        string failure = _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Second", Create);

        Assert.NotNull(failure);
        Assert.Empty(_created);
    }

    [Fact]
    public void AfterACutsceneOpening_TheFirstScreenStillOpensOnce()
    {
        var view = AddView();

        _gameManager.OpenScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Intro", Create);
        _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Title", Create);
        _gameManager.OpenFirstScreen(GameplayExecutionPolicies.Runtime, "Title", Create);

        Assert.Equal(new[] { "Intro", "Title" }, _created);
        Assert.Equal(2, view.Pushed.Count);
    }

    // ---- The factory ----

    [Fact]
    public void WithoutAGame_BothActionsFail_WithEnded_AndTheReasonIsLoggedOnce()
    {
        foreach (CutsceneActionData action in new CutsceneActionData[]
                 {
                     new OpenScreenCutsceneActionData { Target = "Intro" },
                     new ChangeWorldCutsceneActionData { Target = "second.world" },
                 })
        {
            var logger = new CapturingLogger();
            Logs.AddLogger(logger);
            var world = new World();
            var ended = new List<CutsceneEndedEventArgs>();
            world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

            PlayAndStep(world, action);

            Assert.Equal(CutsceneRuntimeState.Failed, world.CutsceneDirector.GetDebugSnapshot().State);
            Assert.Equal(CutsceneRuntimeState.Failed, Assert.Single(ended).State);
            Assert.Single(logger.Errors);
            Logs.Close();
        }
    }

    [Fact]
    public void OpenScreenWithoutAnUnderlyingGame_FailsEvenWhenTheWorldHasAGameManager()
    {
        var (world, _) = NewWorldWithGameManager();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);

        PlayAndStep(world, new OpenScreenCutsceneActionData { Target = "Intro" });

        Assert.Equal(CutsceneRuntimeState.Failed, Assert.Single(ended).State);
    }

    [Fact]
    public void ChangeWorldToAWorldAbsentFromTheCatalogue_Fails_AndAsksForNothing()
    {
        var (world, manager) = NewWorldWithGameManager();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);

        PlayAndStep(world, new ChangeWorldCutsceneActionData { Target = "missing.world" });

        Assert.Equal(CutsceneRuntimeState.Failed, Assert.Single(ended).State);
        Assert.Null(manager.PendingWorldToLoad);
        var error = Assert.Single(logger.Errors);
        Assert.Contains("missing.world", error);
    }

    [Fact]
    public void ChangeWorldToACataloguedWorld_AsksTheGameManager_AndTheCutsceneContinuesInTheSameUpdate()
    {
        var (world, manager) = NewWorldWithGameManager();
        var ended = new List<CutsceneEndedEventArgs>();
        world.CutsceneDirector.Ended += (_, args) => ended.Add(args);
        var logger = new CapturingLogger();
        Logs.AddLogger(logger);

        PlayAndStep(world, new SequenceCutsceneActionData
        {
            Actions =
            {
                new ChangeWorldCutsceneActionData { Target = "second.world" },
                new WaitFramesCutsceneActionData { Frames = 0 },
            },
        });

        Assert.Equal("second.world", manager.PendingWorldToLoad);
        Assert.Equal(CutsceneRuntimeState.Completed, Assert.Single(ended).State);
        Assert.Empty(logger.Errors);
    }

    // ---- Helpers ----

    private static void PlayAndStep(World world, CutsceneActionData root)
    {
        world.CutsceneDirector.Play(new CutsceneAsset { Name = "Chain", RootAction = root });
        Assert.True(world.CutsceneDirector.IsPlaying);
        world.CoroutineManager.Update(new CoroutineUpdateContext(0.1f, 0.1f, 1f, 1));
    }

    private static (World World, GameManager Manager) NewWorldWithGameManager()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var manager = new GameManager(null);
        SetBackingField(game, nameof(CasaEngineGame.GameManager), manager);
        game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;

        var world = new World();
        SetBackingField(world, nameof(World.Game), game);
        return (world, manager);
    }

    private static void SetBackingField(object instance, string propertyName, object value)
    {
        var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private IUIScreen Create(string name, IUIViewRuntime view)
    {
        _created.Add(name);
        return new StubScreen();
    }

    private FirstScreenTests.StubUIViewRuntime AddView()
    {
        var runtime = new FirstScreenTests.StubUIViewRuntime();
        AddViewWithRuntime(runtime);
        return runtime;
    }

    private void AddViewWithRuntime(IUIViewRuntime? runtime)
    {
        var bounds = new Rectangle(0, 0, 320, 200);
        _gameManager.ViewManager.Add(new RenderView(new World(), new ArcBallCameraComponent(), new StubRenderSurface(bounds))
        {
            Host = new StubViewHost(bounds),
            UIView = runtime!,
        });
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Close() { }
        public void WriteTrace(string msg) { }
        public void WriteDebug(string msg) { }
        public void WriteInfo(string msg) { }
        public void WriteWarning(string msg) { }
        public void WriteError(string msg) => Errors.Add(msg);
    }

    private sealed class StubScreen : IUIScreen
    {
        public UILayer Layer => UILayer.Menu;

        public bool IsModal => false;

        public bool BlocksViewsBelow => false;

        public void Initialize(UIRoot root)
        {
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Update(GameTime gameTime)
        {
        }

        public IEnumerable<MGWindow> GetWindows() => Enumerable.Empty<MGWindow>();
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public StubRenderSurface(Rectangle viewportRect)
        {
            ViewportRect = viewportRect;
        }

        public bool IsBackBuffer => false;

        public Rectangle ViewportRect { get; }

        public RenderTarget2D? RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice)
        {
        }

        public void Restore(GraphicsDevice graphicsDevice)
        {
        }
    }

    private sealed class StubViewHost : IViewHost, IViewScreenBoundsHost
    {
        public StubViewHost(Rectangle screenBounds)
        {
            ScreenBounds = screenBounds;
        }

        public ViewId ViewId => ViewId.Empty;

        public int Width => ScreenBounds.Width;

        public int Height => ScreenBounds.Height;

        public bool IsVisible => true;

        public Rectangle ScreenBounds { get; }

        public event Action<IViewHost, int, int>? Resized;

        public event Action<IViewHost>? Closed;

        public void NotifyResized(int newWidth, int newHeight)
        {
            Resized?.Invoke(this, newWidth, newHeight);
        }

        public void Dispose()
        {
            Closed?.Invoke(this);
        }
    }
}
