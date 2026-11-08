using System.Collections;
using System.Reflection;
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
using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Cutscenes;

/// <summary>
/// The PlayVideo cutscene action: JSON, validation, the GameManager method that opens the screen, and the coroutine
/// that waits for the videos and removes the screen. The catalogue is static, so this class runs in
/// <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class PlayVideoCutsceneActionTests : IDisposable
{
    private const string Header = """xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="320" Height="200" """;
    private static readonly Guid ScreenId = Guid.Parse("33333333-aaaa-4aaa-8aaa-333333333333");

    private readonly string _root = Directory.CreateTempSubdirectory("play-video-").FullName;
    private readonly string _envelopePath;
    private readonly MGDesktop _desktop;
    private readonly HeadlessUiTestHarness.HeadlessRuntime _runtime;
    private readonly ScreenStack _stack = new(null);
    private readonly List<FakeVideo> _videos = new();
    private readonly HashSet<string> _ended = new();
    private readonly GameManager _gameManager = new(null);
    private int _frame;

    // The element may ask for its source again, so every video made for the name ends.
    private void End(string name)
    {
        _ended.Add(name);
        _videos.ForEach(video => video.Ends |= video.Name == name);
    }

    public PlayVideoCutsceneActionTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ScreenId) { Name = "Clip", FileName = "clip.uiscreen", AssetType = "uiscreen" });
        _envelopePath = Path.Combine(_root, "clip.uiscreen");
        var inner = HeadlessUiTestHarness.NewDesktop().Runtime.AssetProvider;
        (_desktop, _runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768, new VideoProvider(inner, _videos, _ended));
    }

    public void Dispose()
    {
        Logs.Close();
        AssetCatalog.ClearInternal();
        Directory.Delete(_root, recursive: true);
    }

    // ---- JSON and validation ----

    [Fact]
    public void SaveLoad_RoundTripsThePlayVideoAction()
    {
        var asset = new CutsceneAsset
        {
            RootAction = new SequenceCutsceneActionData
            {
                Actions = { new PlayVideoCutsceneActionData { Target = "Clip" } },
            },
        };
        var node = new JObject();

        CutsceneAssetJsonSerializer.Save(asset, node);
        var loaded = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(loaded, node);

        var root = Assert.IsType<SequenceCutsceneActionData>(loaded.RootAction);
        Assert.Equal("Clip", Assert.IsType<PlayVideoCutsceneActionData>(root.Actions[0]).Target);
        Assert.Equal("PlayVideo", node["root_action"]!["actions"]![0]!["type"]!.ToString());
        Assert.True(loaded.Validate().IsValid);
    }

    [Theory]
    [InlineData("{\"type\":\"PlayVideo\"}")]
    [InlineData("{\"type\":\"PlayVideo\",\"target\":\"\"}")]
    [InlineData("{\"type\":\"PlayVideo\",\"target\":\"  \"}")]
    [InlineData("{\"type\":\"PlayVideo\",\"target\":3}")]
    public void ATargetThatIsAbsentOrEmpty_IsAValidatorErrorNamingTheActionAndTheField(string json)
    {
        var node = new JObject { ["version"] = 1, ["root_action"] = JObject.Parse(json) };
        var asset = new CutsceneAsset();
        CutsceneAssetJsonSerializer.Load(asset, node);

        var error = Assert.Single(asset.Validate().Messages, message => message.Severity == CutsceneValidationSeverity.Error);

        Assert.Contains("PlayVideo.target", error.Message);
    }

    // ---- GameManager ----

    [Fact]
    public void InTheRuntime_TheScreenAndItsViewAreGivenBack()
    {
        var view = AddView();
        var screen = BuildScreen("""<Video Name="vid" SourceName="clip" />""");

        string? failure = _gameManager.OpenVideoScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Clip", (_, _) => screen, out var opened, out var openedView);

        Assert.Null(failure);
        Assert.Same(screen, opened);
        Assert.Same(view, openedView);
        Assert.Single(view.Pushed);
    }

    [Fact]
    public void UnderExternalViewManagement_NothingOpens_AndNoFailureIsReturned()
    {
        var view = AddView();

        string? failure = _gameManager.OpenVideoScreenFromCutscene(GameplayExecutionPolicies.EditorPreview, "Clip", (_, _) => throw new InvalidOperationException(), out var opened, out var openedView);

        Assert.Null(failure);
        Assert.Null(opened);
        Assert.Null(openedView);
        Assert.Empty(view.Pushed);
    }

    [Fact]
    public void AScreenAbsentFromTheCatalogue_NamesTheActionAndTheScreen()
    {
        AddView();

        string? failure = _gameManager.OpenVideoScreenFromCutscene(GameplayExecutionPolicies.Runtime, "Missing", (_, _) => null!, out var opened, out _);

        Assert.Contains("PlayVideo", failure);
        Assert.Contains("Missing", failure);
        Assert.Null(opened);
    }

    // ---- The coroutine ----

    [Fact]
    public void TheActionWaitsForEveryVideo_ThenRemovesTheScreen()
    {
        var run = Start(out var view, """<Video Name="vid" SourceName="a" Width="50" Height="50" /><Video Name="vid2" SourceName="b" Width="50" Height="50" />""");

        Step(run);
        Assert.False(run.IsDone);
        Assert.Empty(view.Removed);

        End("a");
        Step(run);
        Assert.False(run.IsDone);
        Assert.Empty(view.Removed);

        End("b");
        Step(run);
        Step(run);

        Assert.True(run.IsDone);
        Assert.Same(run.Screen, Assert.Single(view.Removed));
        Assert.Equal(CutsceneRuntimeState.Playing, run.Director.GetDebugSnapshot().State);
    }

    [Fact]
    public void ASkipEndsTheWait()
    {
        var run = Start(out var view, """<Video Name="vid" SourceName="a" IsSkippable="True" />""");
        Step(run);
        Assert.False(run.IsDone);

        Assert.True(UIRoot.SkipVideosOfTopScreen(_stack));
        Step(run);

        Assert.True(run.IsDone);
        Assert.Single(view.Removed);
    }

    [Fact]
    public void AScreenWithoutAVideo_FailsThePlay_AndRemovesTheScreen()
    {
        var run = Start(out var view, """<TextBlock Name="lbl" Text="None" />""");

        Step(run);

        var snapshot = run.Director.GetDebugSnapshot();
        Assert.Equal(CutsceneRuntimeState.Failed, snapshot.State);
        Assert.Contains("no Video", snapshot.ActiveActionStopReason);
        Assert.Single(view.Removed);
    }

    [Fact]
    public void UnderExternalViewManagement_TheActionEndsAtOnce()
    {
        var view = AddView();
        var run = new Run(new World(), null);
        run.Begin(target =>
        {
            string? failure = _gameManager.OpenVideoScreenFromCutscene(
                GameplayExecutionPolicies.EditorSimulation, target, (_, _) => throw new InvalidOperationException(), out var screen, out var openedView);
            return (failure, screen, openedView);
        });

        Step(run);

        Assert.True(run.IsDone);
        Assert.Empty(view.Pushed);
        Assert.Empty(view.Removed);
        Assert.Equal(CutsceneRuntimeState.Playing, run.Director.GetDebugSnapshot().State);
    }

    [Fact]
    public void StoppingTheCutsceneEarly_RemovesTheScreen()
    {
        var run = Start(out var view, """<Video Name="vid" SourceName="a" Width="50" Height="50" />""");
        Step(run);
        Assert.Empty(view.Removed);

        run.Director.Stop();
        Step(run);

        Assert.Same(run.Screen, Assert.Single(view.Removed));
    }

    // ---- Helpers ----

    private Run Start(out ViewStub view, string content)
    {
        view = AddView();
        var screen = BuildScreen(content);
        var run = new Run(new World(), screen);
        var viewForRun = view;
        run.Begin(target => (null, screen, viewForRun));
        _stack.PushBuilt(screen);
        view.OnRemove = removed => _stack.RemoveBuilt(removed);
        return run;
    }

    private void Step(Run run)
    {
        _frame++;
        _runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(16 * _frame),
            TimeSpan.FromMilliseconds(16),
            new MouseState(-100, -100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            default));
        UIRoot.RunFrame(_desktop, null!, _stack, new GameTime(TimeSpan.FromMilliseconds(16 * _frame), TimeSpan.FromMilliseconds(16)));
        run.World.RuntimeSystems.CoroutineManager.Update(new CoroutineUpdateContext(0.016f, 0.016f, 1f, _frame));
    }

    private DataDrivenScreen BuildScreen(string content)
    {
        File.WriteAllText(Path.Combine(_root, "clip.xaml"), $"<Window {Header}><StackPanel>{content}</StackPanel></Window>");
        var asset = new UIScreenAsset { SourceXamlFile = "clip.xaml" };
        var context = new UIScreenActionContext
        {
            ScreenExists = _ => false,
            WorldExists = _ => false,
            AcquireSound = _ => throw new NotSupportedException(),
            IsTopScreen = screen => ReferenceEquals(_stack.Top, screen),
            OpenScreen = _ => { },
            CloseScreen = _ => { },
            ChangeWorld = _ => { },
            PlaySound = _ => { },
            Quit = () => { },
            StartFade = _ => { },
            IsFadeRunning = () => false,
        };
        var screen = new DataDrivenScreen(asset, _envelopePath, context);
        _desktop.Windows.Add(screen.BuildWindow(_desktop));
        return screen;
    }

    private ViewStub AddView()
    {
        var view = new ViewStub();
        var bounds = new Rectangle(0, 0, 320, 200);
        _gameManager.ViewManager.Add(new RenderView(new World(), new ArcBallCameraComponent(), new StubRenderSurface(bounds))
        {
            Host = new StubViewHost(bounds),
            UIView = view,
        });
        return view;
    }

    /// <summary>One play of the action: a director with a play in progress, and the action coroutine owned by it.</summary>
    private sealed class Run
    {
        private CoroutineHandle _handle;

        public Run(World world, DataDrivenScreen? screen)
        {
            World = world;
            Screen = screen;
        }

        public World World { get; }

        public DataDrivenScreen? Screen { get; }

        public CutsceneDirector Director => World.CutsceneDirector;

        public bool IsDone => !World.RuntimeSystems.CoroutineManager.IsRunning(_handle);

        public void Begin(Func<string, (string? Failure, IUIScreen? Screen, IUIViewRuntime? View)> open)
        {
            // A play in progress gives the director a token the action can fail against.
            Director.Play(new CutsceneAsset { Name = "Host", RootAction = new WaitCutsceneActionData { Seconds = 1000f } });
            int token = (int)typeof(CutsceneDirector).GetField("_playToken", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Director)!;
            IEnumerator action = CutsceneActionCoroutineFactory.PlayVideoScreen("Clip", open!, Director, token);
            _handle = World.RuntimeSystems.CoroutineManager.StartCoroutine(action, Director, "PlayVideo");
        }
    }

    private sealed class ViewStub : IUIViewRuntime
    {
        public List<IUIScreen> Pushed { get; } = new();

        public List<IUIScreen> Removed { get; } = new();

        public Action<IUIScreen>? OnRemove { get; set; }

        public bool IsPointerOverUI => false;

        public bool IsPointerCaptured => false;

        public bool IsKeyboardCaptured => false;

        public UIViewInputState InputState => new(false, false, false, false);

        public bool HasModalInput => false;

        public UIViewMetrics Metrics { get; private set; } = new(new Point(1, 1), new Point(1, 1), 1.0f, Rectangle.Empty);

        public void Update(GameTime gameTime) { }

        public void Draw() { }

        public void UpdateMetrics(UIViewMetrics metrics) => Metrics = metrics;

        public void PushScreen(IUIScreen screen) => Pushed.Add(screen);

        public IUIScreen? PopScreen() => null;

        public void RemoveScreen(IUIScreen screen)
        {
            Removed.Add(screen);
            OnRemove?.Invoke(screen);
        }

        public void Dispose() { }
    }

    private sealed class VideoProvider(IUIAssetProvider inner, List<FakeVideo> created, HashSet<string> ended) : IUIAssetProvider
    {
        public IUIImageResource LoadImage(string assetName) => inner.LoadImage(assetName);

        public bool TryLoadImage(string assetName, out IUIImageResource image) => inner.TryLoadImage(assetName, out image);

        public bool TryCreateVideo(string name, out IUIVideo video)
        {
            var fake = new FakeVideo { Name = name, Ends = ended.Contains(name) };
            created.Add(fake);
            video = fake;
            return true;
        }
    }

    /// <summary>Never ends by itself, and shows no image.</summary>
    private sealed class FakeVideo : IUIVideo
    {
        public string Name { get; init; } = "";

        public bool Ends { get; set; }

        public IUIImageResource CurrentImage => null!;

        public Rectangle? CurrentSourceRect => null;

        public bool IsEnded => Ends;

        public void Advance(TimeSpan elapsed) { }

        public void Restart() => Ends = false;

        public void Dispose() { }
    }

    private sealed class StubRenderSurface : IRenderSurface
    {
        public StubRenderSurface(Rectangle viewportRect) => ViewportRect = viewportRect;

        public bool IsBackBuffer => false;

        public Rectangle ViewportRect { get; }

        public RenderTarget2D? RenderTarget => null;

        public void Apply(GraphicsDevice graphicsDevice) { }

        public void Restore(GraphicsDevice graphicsDevice) { }
    }

    private sealed class StubViewHost : IViewHost, IViewScreenBoundsHost
    {
        public StubViewHost(Rectangle screenBounds) => ScreenBounds = screenBounds;

        public ViewId ViewId => ViewId.Empty;

        public int Width => ScreenBounds.Width;

        public int Height => ScreenBounds.Height;

        public bool IsVisible => true;

        public Rectangle ScreenBounds { get; }

        public event Action<IViewHost, int, int>? Resized;

        public event Action<IViewHost>? Closed;

        public void NotifyResized(int newWidth, int newHeight) => Resized?.Invoke(this, newWidth, newHeight);

        public void Dispose() => Closed?.Invoke(this);
    }
}
