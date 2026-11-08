using CasaEngine.Engine.Input;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The videos of a data-driven screen: the commands they name are checked at load, and a press of Submit or Cancel
/// skips the playing ones of the screen at the top of the stack, through the same code <c>UIRoot.Update</c> runs.
/// </summary>
public sealed class DataDrivenScreenVideoTests : IDisposable
{
    private const string Header = """xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="320" Height="200" """;

    private readonly string _root = Directory.CreateTempSubdirectory("data-driven-video-").FullName;
    private readonly string _envelopePath;
    private readonly List<string> _calls = new();
    private readonly List<string> _events = new();
    private readonly MGDesktop _desktop;
    private readonly HeadlessUiTestHarness.HeadlessRuntime _runtime;
    private readonly UIScreenActionContext _context;
    private readonly ScreenStack _stack = new(null);
    private int _frame;

    public DataDrivenScreenVideoTests()
    {
        _envelopePath = Path.Combine(_root, "video.uiscreen");
        var inner = HeadlessUiTestHarness.NewDesktop().Runtime.AssetProvider;
        (_desktop, _runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768, new VideoProvider(inner));

        _context = new UIScreenActionContext
        {
            ScreenExists = _ => false,
            WorldExists = _ => false,
            AcquireSound = _ => throw new NotSupportedException(),
            IsTopScreen = screen => ReferenceEquals(_stack.Top, screen),
            OpenScreen = _ => { },
            CloseScreen = _ => _calls.Add("close"),
            ChangeWorld = _ => { },
            PlaySound = _ => { },
            Quit = () => _calls.Add("quit"),
            StartFade = _ => { },
            IsFadeRunning = () => false,
            HasUIInputMapping = true,
        };
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AVideo_NamingAnUndeclaredEndedCommand_IsRefusedAtLoad()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build("video.xaml", Markup("""<Video Name="vid" SourceName="clip" EndedCommandName="Missing" />""")));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'Missing'", error.Message);
    }

    [Fact]
    public void AVideo_NamingAnUndeclaredSkippedCommand_IsRefusedAtLoad()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build("video.xaml", Markup("""<Video Name="vid" SourceName="clip" IsSkippable="True" SkippedCommandName="Gone" />""")));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'Gone'", error.Message);
    }

    [Fact]
    public void AVideo_NamingDeclaredCommands_Loads()
    {
        var screen = Build("video.xaml", Markup("""<Video Name="vid" SourceName="clip" EndedCommandName="Play" SkippedCommandName="Back" />"""));

        Assert.NotNull(screen);
    }

    [Fact]
    public void SubmitThenCancel_SkipTheVideoOfTheTopScreen_AndRaiseSkipped()
    {
        var screen = Build("video.xaml", SkippableVideo());
        var navigation = Open(screen);
        var video = Video(screen);
        Assert.True(video.IsPlaying);

        Tap(navigation, Keys.Z);

        Assert.False(video.IsPlaying);
        Assert.Equal(new[] { "skipped" }, _events);

        video.Restart();
        _events.Clear();

        Tap(navigation, Keys.X);

        Assert.False(video.IsPlaying);
        Assert.Equal(new[] { "skipped" }, _events);
    }

    [Fact]
    public void ASkippedPress_IsConsumed_SoTheButtonIsNotClicked_AndTheCancelCommandDoesNotRun()
    {
        var screen = Build("video.xaml", SkippableVideo());
        var navigation = Open(screen);

        Tap(navigation, Keys.Z);
        Assert.Empty(_calls);

        Video(screen).Restart();
        Tap(navigation, Keys.X);
        Assert.Empty(_calls);
    }

    [Fact]
    public void ANotSkippableVideo_IsNotSkipped_AndThePressGoesToTheButton()
    {
        var screen = Build("video.xaml", Markup("""<Video Name="vid" SourceName="clip" />"""));
        var navigation = Open(screen);
        var video = Video(screen);

        Tap(navigation, Keys.Z);

        Assert.True(video.IsPlaying);
        Assert.Empty(_events);
        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Fact]
    public void AVideo_OfAScreenNotAtTheTop_IsNotSkipped()
    {
        var below = Build("video.xaml", SkippableVideo());
        var above = Build("other.xaml", Markup("""<TextBlock Name="lbl" Text="Over" />"""));
        var navigation = Open(below, above);
        var video = Video(below);

        Tap(navigation, Keys.Z);
        Tap(navigation, Keys.X);

        Assert.True(video.IsPlaying);
        Assert.Empty(_events);
    }

    [Fact]
    public void WithoutAVideoToSkip_SubmitClicksTheButton_AndCancelRunsTheCancelCommand()
    {
        var screen = Build("video.xaml", Markup("""<TextBlock Name="lbl" Text="No video" />"""));
        var navigation = Open(screen);

        Tap(navigation, Keys.Z);
        Assert.Equal(new[] { "quit" }, _calls);

        Tap(navigation, Keys.X);
        Assert.Equal(new[] { "quit", "close" }, _calls);
    }

    [Fact]
    public void AVideoThatAlreadyEnded_IsNotSkipped_SoThePressGoesOn()
    {
        var screen = Build("video.xaml", SkippableVideo());
        var navigation = Open(screen);
        Video(screen);
        FakeVideo.Last!.Ends = true;
        Frames(navigation, 2);

        Tap(navigation, Keys.Z);

        Assert.Empty(_events);
        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Fact]
    public void AHeldKey_AsksOnItsPressOnly_NotOnWhatFollows()
    {
        var screen = Build("video.xaml", Markup("""<Video Name="vid" SourceName="clip" />"""));
        var navigation = Open(screen);
        var video = Video(screen);
        int presses = 0;
        navigation.SubmitOrCancelPressed = () =>
        {
            presses++;
            return UIRoot.SkipVideosOfTopScreen(_stack);
        };

        // Held for far longer than the repeat delay of a direction.
        Frames(navigation, 60, new KeyboardState(Keys.Z));
        Assert.Equal(1, presses);

        video.IsSkippable = true;
        Frames(navigation, 60, new KeyboardState(Keys.Z));

        Assert.Equal(1, presses);
        Assert.True(video.IsPlaying);
        Assert.Empty(_events);
    }

    [Fact]
    public void TheHook_IsCalledOnPressesOfSubmitAndCancelOnly_ThroughTheInjectedReader()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var navigation = new UINavigationInput(desktop, Mapping());
        var held = new HashSet<Keys>();
        int count = 0;
        navigation.SubmitOrCancelPressed = () =>
        {
            count++;
            return false;
        };
        bool IsHeld(KeyButton input) => held.Contains(input.Key);
        int ms = 0;
        void Step() => navigation.Update(TimeSpan.FromMilliseconds(ms += 16), IsHeld);

        Step();
        held.Add(Keys.Down);
        for (int i = 0; i < 40; i++)
        {
            Step();
        }

        Assert.Equal(0, count);

        held.Clear();
        Step();
        held.Add(Keys.Z);
        for (int i = 0; i < 40; i++)
        {
            Step();
        }

        Assert.Equal(1, count);

        held.Clear();
        Step();
        held.Add(Keys.X);
        Step();

        Assert.Equal(2, count);
    }

    [Fact]
    public void WhenTheHookConsumes_CancelNotHandledIsNotCalled_AndWhenItDoesNot_ItIs()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var navigation = new UINavigationInput(desktop, Mapping());
        int notHandled = 0;
        navigation.CancelNotHandled = () => notHandled++;
        var held = new HashSet<Keys>();
        bool IsHeld(KeyButton input) => held.Contains(input.Key);

        navigation.SubmitOrCancelPressed = () => true;
        navigation.Update(TimeSpan.FromMilliseconds(16), IsHeld);
        held.Add(Keys.X);
        navigation.Update(TimeSpan.FromMilliseconds(32), IsHeld);
        Assert.Equal(0, notHandled);

        navigation.SubmitOrCancelPressed = () => false;
        held.Clear();
        navigation.Update(TimeSpan.FromMilliseconds(48), IsHeld);
        held.Add(Keys.X);
        navigation.Update(TimeSpan.FromMilliseconds(64), IsHeld);
        Assert.Equal(1, notHandled);
    }

    [Fact]
    public void HidingTheScreen_DisposesItsVideos()
    {
        var screen = Build("video.xaml", SkippableVideo());
        var video = FakeVideo.Last!;
        Open(screen);
        Assert.False(video.IsDisposed);
        Assert.Equal(1, screen.VideoCount);

        screen.Hide();

        Assert.True(video.IsDisposed);
        Assert.Equal(0, screen.VideoCount);
        Assert.False(screen.IsAnyVideoPlaying);
    }

    [Fact]
    public void DisposingAScreen_ThatFailedToLoad_DisposesTheVideosItsXamlCreated()
    {
        File.WriteAllText(Path.Combine(_root, "video.xaml"), Markup("""<Video Name="vid" SourceName="clip" EndedCommandName="Missing" />"""));
        var asset = new UIScreenAsset { SourceXamlFile = "video.xaml", DefaultFocus = "btnPlay" };
        asset.UICommands.Add(Command("Play", "quit"));
        var screen = new DataDrivenScreen(asset, _envelopePath, _context);

        Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));
        var video = FakeVideo.Last!;
        Assert.False(video.IsDisposed);

        screen.Dispose();

        Assert.True(video.IsDisposed);
    }

    [Fact]
    public void AScreenThatFailsBeforeItsVideosAreChecked_StillReleasesThem()
    {
        File.WriteAllText(Path.Combine(_root, "video.xaml"), Markup("""<Video Name="vid" SourceName="clip" />"""));
        var asset = new UIScreenAsset { SourceXamlFile = "video.xaml", DefaultFocus = "btnPlay", CancelCommand = "Missing" };
        asset.UICommands.Add(Command("Play", "quit"));
        var screen = new DataDrivenScreen(asset, _envelopePath, _context);

        Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));
        var video = FakeVideo.Last!;

        screen.Dispose();

        Assert.True(video.IsDisposed);
    }

    [Fact]
    public void AScreenWhoseEnvelopeHasFormatErrors_StillReleasesItsVideos()
    {
        File.WriteAllText(Path.Combine(_root, "video.xaml"), Markup("""<Video Name="vid" SourceName="clip" />"""));
        var asset = new UIScreenAsset { SourceXamlFile = "video.xaml", DefaultFocus = "btnPlay" };
        asset.UICommands.Add(Command("Play", "quit"));
        asset.CommandFormatErrors.Add("is_modal must be a boolean.");
        var screen = new DataDrivenScreen(asset, _envelopePath, _context);

        Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));
        var video = FakeVideo.Last!;

        screen.Dispose();

        Assert.True(video.IsDisposed);
    }

    private static string SkippableVideo()
        => Markup("""<Video Name="vid" SourceName="clip" IsSkippable="True" />""");

    private static string Markup(string content)
        => $"""
            <Window {Header}>
              <StackPanel Orientation="Vertical" Spacing="8" Padding="16">
                <Button Name="btnPlay" CommandName="Play" Content="Play" />
                {content}
              </StackPanel>
            </Window>
            """;

    private DataDrivenScreen Build(string xamlFile, string markup)
    {
        File.WriteAllText(Path.Combine(_root, xamlFile), markup);
        var asset = new UIScreenAsset { SourceXamlFile = xamlFile, DefaultFocus = "btnPlay", CancelCommand = "Back" };
        asset.UICommands.Add(Command("Play", "quit"));
        asset.UICommands.Add(Command("Back", "close_screen"));

        var screen = new DataDrivenScreen(asset, _envelopePath, _context);
        _desktop.Windows.Add(screen.BuildWindow(_desktop));
        return screen;
    }

    private static UIScreenCommandData Command(string name, string actionType)
    {
        var holder = new UIScreenAsset();
        holder.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "holder",
            ["ui_commands"] = new JArray(new JObject
            {
                ["name"] = name,
                ["actions"] = new JArray(new JObject { ["type"] = actionType }),
            }),
        });

        return Assert.Single(holder.UICommands);
    }

    private MGVideo Video(DataDrivenScreen screen)
    {
        var window = Assert.Single(screen.GetWindows());
        Assert.True(window.TryGetElementByName("vid", out MGVideo video));
        video.Skipped += (_, _) => _events.Add("skipped");
        return video;
    }

    /// <summary>Pushes the screens, the last one at the top, and settles the frames. The mapping is the real one.</summary>
    private UINavigationInput Open(params DataDrivenScreen[] screens)
    {
        foreach (var screen in screens)
        {
            _stack.PushBuilt(screen);
        }

        var navigation = new UINavigationInput(_desktop, Mapping());
        if (_stack.Top is DataDrivenScreen top)
        {
            navigation.CancelNotHandled = top.ReportCancel;
        }

        navigation.SubmitOrCancelPressed = () => UIRoot.SkipVideosOfTopScreen(_stack);
        Frames(navigation, 4);
        return navigation;
    }

    private void Tap(UINavigationInput navigation, Keys key)
    {
        Frames(navigation, 1, new KeyboardState(key));
        Frames(navigation, 1);
    }

    private void Frames(UINavigationInput navigation, int count, KeyboardState keyboard = default)
    {
        for (int i = 0; i < count; i++)
        {
            _frame++;
            _runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(16 * _frame),
                TimeSpan.FromMilliseconds(16),
                new MouseState(-100, -100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                keyboard));
            UIRoot.RunFrame(_desktop, navigation, _stack, new GameTime(TimeSpan.FromMilliseconds(16 * _frame), TimeSpan.FromMilliseconds(16)));
        }
    }

    private static ButtonsMapping Mapping()
    {
        var mapping = new ButtonsMapping { Name = "ui-mapping" };
        mapping.Buttons.Add(Entry("Submit", Keys.Z));
        mapping.Buttons.Add(Entry("Cancel", Keys.X));
        mapping.Buttons.Add(Entry("NavigateUp", Keys.Up));
        mapping.Buttons.Add(Entry("NavigateDown", Keys.Down));
        mapping.Buttons.Add(Entry("NavigateLeft", Keys.Left));
        mapping.Buttons.Add(Entry("NavigateRight", Keys.Right));
        return mapping;
    }

    private static InputMapping Entry(string name, Keys key)
        => new() { Name = name, KeyButton = new KeyButton(key), AlternativeKeyButton = new KeyButton() };

    private sealed class VideoProvider(IUIAssetProvider inner) : IUIAssetProvider
    {
        public IUIImageResource LoadImage(string assetName) => inner.LoadImage(assetName);

        public bool TryLoadImage(string assetName, out IUIImageResource image) => inner.TryLoadImage(assetName, out image);

        public bool TryCreateVideo(string name, out IUIVideo video)
        {
            video = FakeVideo.Last = new FakeVideo();
            return true;
        }
    }

    /// <summary>Never ends by itself, and shows no image.</summary>
    private sealed class FakeVideo : IUIVideo
    {
        public static FakeVideo? Last { get; set; }

        public bool Ends { get; set; }

        public IUIImageResource CurrentImage => null!;

        public Rectangle? CurrentSourceRect => null;

        public bool IsEnded => Ends;

        public void Advance(TimeSpan elapsed)
        {
        }

        public void Restart() => Ends = false;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
