using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// A screen with no code, driven by its envelope (ADR-0078), on a headless desktop. The game is the recording context
/// below: a <see cref="UIRoot"/> cannot exist without a graphics device.
/// </summary>
public sealed class DataDrivenScreenTests : IDisposable
{
    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Orientation="Vertical" Spacing="8" Padding="16">
            <TextBlock Name="lblTitle" Text="Menu" />
            <Button Name="btnPlain" Content="Plain" />
            <Button Name="btnPlay" CommandName="Play" Content="Play" />
          </StackPanel>
        </Window>
        """;

    private static readonly Point Away = new(-100, -100);

    private readonly string _root = Directory.CreateTempSubdirectory("data-driven-screen-").FullName;
    private readonly string _envelopePath;
    private readonly List<string> _calls = new();
    private readonly List<AssetHandle<SoundAsset>> _soundHandles = new();
    private readonly AssetContentManager _assets = new();
    private readonly Dictionary<string, Guid> _soundIds = new();
    private readonly List<IDisposable> _ownHolds = new();
    private readonly MGDesktop _desktop;
    private readonly HeadlessUiTestHarness.HeadlessRuntime _runtime;
    private readonly UIScreenActionContext _context;
    private bool _isTop = true;
    private int _frame;
    private Action<IUIScreen> _onClose = static _ => { };

    public DataDrivenScreenTests()
    {
        _envelopePath = Path.Combine(_root, "menu.uiscreen");
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), Markup);
        (_desktop, _runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        AddSound("Confirm");

        _context = new UIScreenActionContext
        {
            ScreenExists = name => name == "Options",
            WorldExists = name => name == "Level1.world",
            AcquireSound = name =>
            {
                var handle = _assets.Acquire<SoundAsset>(_soundIds[name]);
                _soundHandles.Add(handle);
                return handle;
            },
            IsTopScreen = _ => _isTop,
            OpenScreen = name => _calls.Add($"open:{name}"),
            CloseScreen = screen =>
            {
                _calls.Add("close");
                _onClose(screen);
            },
            ChangeWorld = name => _calls.Add($"world:{name}"),
            PlaySound = sound => _calls.Add($"sound:{sound.Name}"),
            Quit = () => _calls.Add("quit"),
        };
    }

    public void Dispose()
    {
        foreach (var hold in _ownHolds)
        {
            hold.Dispose();
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void AClick_RunsTheWholeListInOrder_OnTheNextUpdate_NotBefore()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(
            ("play_sound", "Confirm"), ("open_screen", "Options"), ("change_world", "Level1.world"), ("close_screen", null), ("quit", null)));
        Settle(screen);

        Click(ButtonCentre(screen, "btnPlay"), screen, updateScreen: false);
        Assert.Empty(_calls);

        screen.Update(new GameTime());

        Assert.Equal(new[] { "sound:Confirm", "open:Options", "world:Level1.world", "close", "quit" }, _calls);
    }

    [Fact]
    public void Enter_RunsTheSameListAsAClick()
    {
        var clicked = Build(new UIScreenAsset(), PlayCommand(("quit", null), ("open_screen", "Options")));
        Settle(clicked);
        Click(ButtonCentre(clicked, "btnPlay"), clicked);
        var afterClick = _calls.ToArray();
        _calls.Clear();

        var focused = new UIScreenAsset { DefaultFocus = "btnPlay" };
        var entered = BuildOn(NewDesktop(out var runtime), runtime, focused, PlayCommand(("quit", null), ("open_screen", "Options")));
        Settle(entered);
        PressEnter(Away, entered);

        Assert.Equal(new[] { "quit", "open:Options" }, afterClick);
        Assert.Equal(afterClick, _calls);
    }

    [Fact]
    public void ACommand_RunsOnce()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(screen);

        Click(ButtonCentre(screen, "btnPlay"), screen);
        screen.Update(new GameTime());
        screen.Update(new GameTime());

        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Fact]
    public void AButtonWithoutACommand_IsLegal_AndRunsNothing()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(screen);

        Click(ButtonCentre(screen, "btnPlain"), screen);

        Assert.Empty(_calls);
    }

    [Fact]
    public void TheDefaultFocus_IsAppliedOnceTheLayoutIsDone()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));

        Settle(screen);

        Assert.Same(Button(screen, "btnPlay"), _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void TheDefaultFocus_TargetsTheNamedButton_NotTheFirstOne()
    {
        // MGUI's auto-focus would pick btnPlain, which comes first.
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(screen);

        PressEnter(Away, screen);

        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Fact]
    public void TheDefaultFocus_IsGivenOnce_AndDoesNotTakeTheFocusBack()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(screen);
        var other = Button(screen, "btnPlain");

        other.Focus(KeyboardFocusSource.Programmatic);
        Settle(screen);

        Assert.Same(other, _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void EnterUnderAHoveredButton_RunsTheList_ThanksToTheDefaultFocus()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(screen);
        var centre = ButtonCentre(screen, "btnPlay");
        Step(centre, screen);
        Step(centre, screen);

        PressEnter(centre, screen);

        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Fact]
    public void AnEnvelopeWithNothingInIt_LoadsAScreenThatDoesNothing()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), Markup.Replace(" CommandName=\"Play\"", string.Empty));
        var screen = Build(new UIScreenAsset());
        Settle(screen);

        Click(ButtonCentre(screen, "btnPlay"), screen);

        Assert.Empty(_calls);
        Assert.False(screen.IsModal);
        Assert.Equal(UILayer.Menu, screen.Layer);
    }

    [Fact]
    public void IsModal_ComesFromTheEnvelope()
    {
        var screen = Build(new UIScreenAsset { IsModal = true }, PlayCommand(("quit", null)));

        Assert.True(screen.IsModal);
        Assert.True(screen.BlocksViewsBelow);
        Assert.Equal(UILayer.Modal, screen.Layer);
    }

    [Theory]
    [InlineData("unknown action", """{ "name": "Play", "actions": [ { "type": "wait_fade", "duration": 12 } ] }""", "wait_fade")]
    [InlineData("no type", """{ "name": "Play", "actions": [ { "target": "x" } ] }""", "unknown type")]
    [InlineData("open, no target", """{ "name": "Play", "actions": [ { "type": "open_screen" } ] }""", "open_screen")]
    [InlineData("world, no target", """{ "name": "Play", "actions": [ { "type": "change_world" } ] }""", "change_world")]
    [InlineData("sound, no target", """{ "name": "Play", "actions": [ { "type": "play_sound" } ] }""", "play_sound")]
    [InlineData("unknown screen", """{ "name": "Play", "actions": [ { "type": "open_screen", "target": "Nope" } ] }""", "'Nope'")]
    [InlineData("unknown world", """{ "name": "Play", "actions": [ { "type": "change_world", "target": "Nope.world" } ] }""", "'Nope.world'")]
    [InlineData("unknown sound", """{ "name": "Play", "actions": [ { "type": "play_sound", "target": "Nope" } ] }""", "'Nope'")]
    [InlineData("close with a target", """{ "name": "Play", "actions": [ { "type": "close_screen", "target": "x" } ] }""", "close_screen")]
    [InlineData("quit with a target", """{ "name": "Play", "actions": [ { "type": "quit", "target": "x" } ] }""", "quit")]
    [InlineData("button's command undeclared", """{ "name": "Other", "actions": [] }""", "'Play'")]
    public void InvalidCommands_FailAtLoad_NamingTheEnvelopeAndTheProblem(string _, string commandJson, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build(new UIScreenAsset(), JObject.Parse(commandJson)));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void ACommandWithNoName_FailsAtLoad()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset(), PlayCommand(("quit", null)), JObject.Parse("""{ "actions": [] }""")));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("no name", error.Message);
    }

    [Fact]
    public void ACommandDeclaredTwice_FailsAtLoad()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset(), PlayCommand(("quit", null)), PlayCommand(("quit", null))));

        Assert.Contains("'Play' is declared twice", error.Message);
    }

    [Theory]
    [InlineData("btnNope", "does not declare")]
    [InlineData("lblTitle", "cannot take the focus")]
    public void AnInvalidDefaultFocus_FailsAtLoad(string name, string expected)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset { DefaultFocus = name }, PlayCommand(("quit", null))));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains(name, error.Message);
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void AMalformedEnvelope_FailsAtLoad_WithWhatLoadReported()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse("""{ "id": "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234", "name": "n", "ui_commands": 3 }"""));
        asset.SourceXamlFile = "menu.xaml";

        var screen = new DataDrivenScreen(asset, _envelopePath, _context);
        var error = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'ui_commands'", error.Message);
    }

    [Fact]
    public void ACommandTheHostRegistered_CountsForAButton()
    {
        _desktop.Resources.AddCommand("Play", static _ => { });

        var screen = Build(new UIScreenAsset());

        Assert.NotNull(screen);
    }

    [Fact]
    public void TwoScreens_MayUseTheSameCommandName()
    {
        var first = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        var second = Build(new UIScreenAsset(), PlayCommand(("open_screen", "Options")));

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public void AClick_OffTheTopOfTheStack_IsDropped_EvenWhenTheScreenBecomesTopAgain()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(screen);

        _isTop = false;
        Click(ButtonCentre(screen, "btnPlay"), screen);
        Assert.Empty(_calls);

        _isTop = true;
        screen.Update(new GameTime());

        Assert.Empty(_calls);
    }

    [Fact]
    public void AModalScreen_ComesBackInFront_WhenAWindowBelowIsRaised()
    {
        var below = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        var modal = Build(new UIScreenAsset { IsModal = true }, PlayCommand(("quit", null)));

        // What a click on the window below does in MGUI.
        _desktop.BringToFront(Assert.Single(below.GetWindows()));
        modal.Update(new GameTime());

        Assert.Same(Assert.Single(modal.GetWindows()), _desktop.Windows[^1]);
    }

    [Fact]
    public void AModalScreen_ThatIsNotOnTop_LeavesTheOrderAlone()
    {
        var modal = Build(new UIScreenAsset { IsModal = true }, PlayCommand(("quit", null)));
        var above = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        _isTop = false;

        modal.Update(new GameTime());

        Assert.Same(Assert.Single(above.GetWindows()), _desktop.Windows[^1]);
    }

    [Fact]
    public void ANonModalScreen_LeavesTheOrderAlone()
    {
        var first = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        var second = Build(new UIScreenAsset(), PlayCommand(("quit", null)));

        first.Update(new GameTime());

        Assert.Same(Assert.Single(second.GetWindows()), _desktop.Windows[^1]);
    }

    [Fact]
    public void AClosedScreen_GivesTheKeyboardBackToTheScreenBelow()
    {
        var below = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(below);
        var closing = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(closing);
        var closingWindow = Assert.Single(closing.GetWindows());
        Assert.Same(closingWindow, _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);

        // What ScreenStack.Remove does.
        closing.Hide();
        _desktop.Windows.Remove(closingWindow);

        Step(Away, below);
        PressEnter(Away, below);

        Assert.Same(Assert.Single(below.GetWindows()), _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);
    }

    [Fact]
    public void AClosedScreen_ReleasesTheFocusOfATextBoxToo()
    {
        var below = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(below);
        File.WriteAllText(
            Path.Combine(_root, "menu.xaml"),
            Markup.Replace("""<TextBlock Name="lblTitle" Text="Menu" />""", """<TextBox Name="txtName" />"""));
        var closing = Build(new UIScreenAsset { DefaultFocus = "txtName" }, PlayCommand(("quit", null)));
        Settle(closing);
        var closingWindow = Assert.Single(closing.GetWindows());
        Assert.Same(closingWindow, _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);

        closing.Hide();
        _desktop.Windows.Remove(closingWindow);
        Step(Away, below);

        Assert.NotSame(closingWindow, _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);
    }

    [Fact]
    public void AScreenClosedByItsOwnCommand_ReleasesTheFocus()
    {
        var below = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(below);
        var closing = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("close_screen", null)));
        Settle(closing);
        var closingWindow = Assert.Single(closing.GetWindows());
        _onClose = closed =>
        {
            closed.Hide();
            _desktop.Windows.Remove(closingWindow);
        };

        PressEnter(Away, closing);
        Step(Away, below);

        Assert.Equal(new[] { "close" }, _calls);
        Assert.NotSame(closingWindow, _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);
    }

    [Fact]
    public void Hide_AfterACommandThatDidNotCloseTheScreen_StillDisposes()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("play_sound", "Confirm")));
        Settle(screen);
        Click(ButtonCentre(screen, "btnPlay"), screen);
        Assert.Equal(new[] { "sound:Confirm" }, _calls);

        screen.Hide();

        Assert.True(Assert.Single(_soundHandles).IsDisposed);
    }

    [Fact]
    public void AScreenWithoutADefaultFocus_TakesItWithItsFirstButton_FromTheScreenBelow()
    {
        var below = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(below);

        var above = Build(new UIScreenAsset(), PlayCommand(("open_screen", "Options")));
        Settle(above);

        Assert.Same(Button(above, "btnPlain"), _desktop.FocusedKeyboardHandler);
    }

    [Theory]
    [InlineData("IsEnabled=\"False\"")]
    [InlineData("Visibility=\"Collapsed\"")]
    public void AScreenWithoutADefaultFocus_SkipsAButtonThatCannotTakeIt(string attribute)
    {
        var below = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(below);
        File.WriteAllText(
            Path.Combine(_root, "menu.xaml"),
            Markup.Replace("""<Button Name="btnPlain" """, $"""<Button Name="btnPlain" {attribute} """));

        var above = Build(new UIScreenAsset(), PlayCommand(("open_screen", "Options")));
        Settle(above);

        Assert.Same(Button(above, "btnPlay"), _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AScreenWithoutADefaultFocus_TakesNothing_WhenNoOtherScreenHoldsTheFocus()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("quit", null)));

        Settle(screen);

        Assert.Null(_desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AScreenWithAnExitEffect_DoesNotKeepTheFocusWhenItCloses()
    {
        var below = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(below);
        File.WriteAllText(
            Path.Combine(_root, "menu.xaml"),
            Markup.Replace("""Left="0" Top="0" """, """Left="0" Top="0" ExitEffect="Fade" """));
        var closing = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("close_screen", null)));
        Settle(closing);
        var closingWindow = Assert.Single(closing.GetWindows());
        _onClose = closed =>
        {
            closed.Hide();
            _desktop.Windows.Remove(closingWindow);
        };

        // Enter leaves the desktop in navigation mode, the only mode where MGUI picks a new focus on a close.
        PressEnter(Away, closing);
        Step(Away, below);
        Step(Away, below);

        Assert.Equal(new[] { "close" }, _calls);
        Assert.Same(Assert.Single(below.GetWindows()), _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);
    }

    [Fact]
    public void TheLastScreen_WithAnExitEffect_LeavesNothingFocused_WhenItCloses()
    {
        File.WriteAllText(
            Path.Combine(_root, "menu.xaml"),
            Markup.Replace("""Left="0" Top="0" """, """Left="0" Top="0" ExitEffect="Fade" """));
        var closing = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("close_screen", null)));
        Settle(closing);
        var closingWindow = Assert.Single(closing.GetWindows());
        _onClose = closed =>
        {
            closed.Hide();
            _desktop.Windows.Remove(closingWindow);
        };

        PressEnter(Away, closing);
        Step(Away, closing);

        Assert.Equal(new[] { "close" }, _calls);
        Assert.Null(_desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AScreenWithNoButton_LeavesTheFocusAlone()
    {
        var below = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        Settle(below);
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="320" Height="200">
              <TextBlock Text="Nothing to press" />
            </Window>
            """);

        var above = Build(new UIScreenAsset());
        Settle(above);

        Assert.Same(Button(below, "btnPlay"), _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AHiddenScreen_LeavesAFocusOutsideItsWindowAlone()
    {
        var kept = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        var hidden = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        Settle(kept);

        hidden.Hide();
        Step(Away, kept);

        Assert.Same(Button(kept, "btnPlay"), _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void TheSound_IsHeldOnceForTheScreen_AndGivenBackWhenItIsHidden()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("play_sound", "Confirm"), ("play_sound", "Confirm")));

        var handle = Assert.Single(_soundHandles);
        Assert.False(handle.IsDisposed);

        screen.Hide();

        Assert.True(handle.IsDisposed);
    }

    [Fact]
    public void Hide_GivesTheEnvelopeBack()
    {
        var assetId = Guid.NewGuid();
        var info = new AssetInfo(assetId) { Name = "Menu", FileName = "menu.uiscreen" };
        File.WriteAllText(_envelopePath, $$"""
            {
              "id": "{{assetId}}",
              "name": "Menu",
              "source_xaml_file": "menu.xaml",
              "ui_commands": [ { "name": "Play", "actions": [] } ]
            }
            """);

        var manager = new AssetContentManager
        {
            RuntimeContext = new CasaEngine.Framework.Application.EngineRuntimeContext(
                new CasaEngine.Framework.Configuration.Project.ProjectSettings(), _root, id => id == assetId ? info : null),
        };
        manager.RegisterAssetLoader(typeof(UIScreenAsset), new EnvelopeLoader());

        var screen = new DataDrivenScreen(manager, assetId.ToString(), _context);
        screen.BuildWindow(_desktop);
        Assert.Equal(0, manager.CollectUnreferenced());

        screen.Hide();

        Assert.Equal(1, manager.CollectUnreferenced());
    }

    [Fact]
    public void ASoundAfterTheCloseInTheSameList_StillPlays_AndTheHoldIsGivenBackAtTheEnd()
    {
        var screen = Build(new UIScreenAsset(), PlayCommand(("close_screen", null), ("play_sound", "Confirm")));
        Settle(screen);
        _onClose = closed => closed.Hide(); // What ScreenStack.Remove does.

        Click(ButtonCentre(screen, "btnPlay"), screen);

        Assert.Equal(new[] { "close", "sound:Confirm" }, _calls);
        Assert.True(Assert.Single(_soundHandles).IsDisposed);
    }

    [Fact]
    public void AFailingAction_StillDisposesAScreenItsListClosed()
    {
        var failing = new UIScreenActionContext
        {
            ScreenExists = _context.ScreenExists,
            WorldExists = _context.WorldExists,
            AcquireSound = _context.AcquireSound,
            IsTopScreen = _context.IsTopScreen,
            OpenScreen = _context.OpenScreen,
            CloseScreen = closed => closed.Hide(),
            ChangeWorld = _context.ChangeWorld,
            PlaySound = _context.PlaySound,
            Quit = () => throw new InvalidOperationException("boom"),
        };

        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        var other = new UIScreenAsset { SourceXamlFile = "menu.xaml" };
        other.UICommands.Add(ToCommand(PlayCommand(("close_screen", null), ("play_sound", "Confirm"), ("quit", null))));
        var failingScreen = new DataDrivenScreen(other, _envelopePath, failing);
        desktop.Windows.Add(failingScreen.BuildWindow(desktop));
        Settle(failingScreen, desktop, runtime);
        var handle = _soundHandles[^1];

        var centre = ButtonCentre(failingScreen, "btnPlay");
        Step(centre, failingScreen, desktop, runtime);
        Step(centre, failingScreen, desktop, runtime);
        Step(centre, failingScreen, desktop, runtime, ButtonState.Pressed);
        _frame++;
        runtime.ApplyFrame(Frame(centre, ButtonState.Released));
        desktop.Update();

        Assert.Throws<InvalidOperationException>(() => failingScreen.Update(new GameTime()));
        Assert.True(handle.IsDisposed);
    }

    [Fact]
    public void WithoutADesignResolution_TheWindowKeepsWhatTheXamlDeclares()
    {
        UseWindowAttributes("""Left="5" Top="7" Width="100" Height="60" Scale="2" """);
        var screen = Build(new UIScreenAsset(), PlayCommand(("quit", null)));
        var window = Assert.Single(screen.GetWindows());

        screen.NotifyScreenBounds(new Rectangle(0, 0, 1280, 720));

        Assert.Equal(2f, window.Scale);
        Assert.Equal(5, window.Left);
        Assert.Equal(7, window.Top);
    }

    [Theory]
    [InlineData(960, 540, 3)]
    [InlineData(1280, 720, 4)]
    [InlineData(1000, 600, 3)]
    [InlineData(200, 100, 1)]
    public void TheWindow_TakesTheIntegerFactorOfTheView_AndScalesItsPosition(int width, int height, int factor)
    {
        UseWindowAttributes("""Left="5" Top="7" Width="100" Height="60" """);
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(width, height);

        var screen = BuildOn(desktop, runtime, new UIScreenAsset(), Scaled(), PlayCommand(("quit", null)));

        var window = Assert.Single(screen.GetWindows());
        Assert.Equal(factor, window.Scale);
        Assert.Equal(5 * factor, window.Left);
        Assert.Equal(7 * factor, window.Top);
    }

    [Fact]
    public void AChangeOfBounds_RescalesFromTheXamlPosition()
    {
        UseWindowAttributes("""Left="5" Top="7" Width="100" Height="60" """);
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(960, 540);
        var screen = BuildOn(desktop, runtime, new UIScreenAsset(), Scaled(), PlayCommand(("quit", null)));
        var window = Assert.Single(screen.GetWindows());

        screen.NotifyScreenBounds(new Rectangle(0, 0, 1280, 720));
        Assert.Equal(4f, window.Scale);
        Assert.Equal(20, window.Left);
        Assert.Equal(28, window.Top);

        screen.NotifyScreenBounds(new Rectangle(0, 0, 640, 360));
        Assert.Equal(2f, window.Scale);
        Assert.Equal(10, window.Left);
        Assert.Equal(14, window.Top);
    }

    [Fact]
    public void AScaleInTheXaml_IsOverwritten_UnderADesignResolution()
    {
        UseWindowAttributes("""Left="0" Top="0" Width="100" Height="60" Scale="5" """);
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(960, 540);

        var screen = BuildOn(desktop, runtime, new UIScreenAsset(), Scaled(), PlayCommand(("quit", null)));

        Assert.Equal(3f, Assert.Single(screen.GetWindows()).Scale);
    }

    [Fact]
    public void AClick_ReachesTheButton_WhereTheScaleDrawsIt()
    {
        UseWindowAttributes("""Left="10" Top="6" Width="300" Height="170" """);
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(960, 540);
        var screen = BuildOn(desktop, runtime, new UIScreenAsset(), Scaled(), PlayCommand(("quit", null)));
        Settle(screen);
        var window = Assert.Single(screen.GetWindows());
        var centre = Button(screen, "btnPlay").LayoutBounds.Center;

        // Where the unscaled layout puts the button: the window's top-left corner stays put, the rest moves away from it.
        Click(centre, screen);
        Assert.Empty(_calls);

        var scaled = new Point(window.Left + (centre.X - window.Left) * 3, window.Top + (centre.Y - window.Top) * 3);
        Click(scaled, screen);
        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Theory]
    [InlineData("""ScreenHorizontalAlignment="Center" """)]
    [InlineData("""ScreenVerticalAlignment="Bottom" """)]
    public void AWindowPlacedByAlignment_IsRefused_UnderADesignResolution(string attribute)
    {
        UseWindowAttributes($"""Width="100" Height="60" {attribute}""");
        var (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(960, 540);

        var error = Assert.Throws<InvalidOperationException>(
            () => BuildOn(desktop, runtime, new UIScreenAsset(), Scaled(), PlayCommand(("quit", null))));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("alignment", error.Message);
    }

    [Fact]
    public void AWindowPlacedByAlignment_StaysLegal_WithoutADesignResolution()
    {
        UseWindowAttributes("""Width="100" Height="60" ScreenHorizontalAlignment="Center" """);

        var screen = Build(new UIScreenAsset(), PlayCommand(("quit", null)));

        Assert.True(Assert.Single(screen.GetWindows()).HasScreenPlacement);
    }

    private sealed class EnvelopeLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var asset = new UIScreenAsset();
            asset.Load(JObject.Parse(File.ReadAllText(fileName)));
            return asset;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private void AddSound(string name)
    {
        var id = Guid.NewGuid();
        _soundIds[name] = id;
        _ownHolds.Add(_assets.Register(id, new SoundAsset { Name = name }));
    }

    private static JObject PlayCommand(params (string Type, string? Target)[] actions)
    {
        var actionArray = new JArray();
        foreach (var (type, target) in actions)
        {
            var action = new JObject { ["type"] = type };
            if (target != null)
            {
                action["target"] = target;
            }

            actionArray.Add(action);
        }

        return new JObject { ["name"] = "Play", ["actions"] = actionArray };
    }

    private static UIScreenCommandData ToCommand(JObject command)
    {
        var holder = new UIScreenAsset();
        holder.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "holder",
            ["ui_commands"] = new JArray(command),
        });

        return Assert.Single(holder.UICommands);
    }

    private DataDrivenScreen Build(UIScreenAsset asset, params JObject[] commands)
        => BuildOn(_desktop, _runtime, asset, commands);

    private DataDrivenScreen BuildOn(MGDesktop desktop, HeadlessUiTestHarness.HeadlessRuntime runtime, UIScreenAsset asset, params JObject[] commands)
        => BuildOn(desktop, runtime, asset, _context, commands);

    /// <summary>The recording context, for a project that scales its screens to 320x180.</summary>
    private UIScreenActionContext Scaled()
        => new()
        {
            ScreenExists = _context.ScreenExists,
            WorldExists = _context.WorldExists,
            AcquireSound = _context.AcquireSound,
            IsTopScreen = _context.IsTopScreen,
            OpenScreen = _context.OpenScreen,
            CloseScreen = _context.CloseScreen,
            ChangeWorld = _context.ChangeWorld,
            PlaySound = _context.PlaySound,
            Quit = _context.Quit,
            DesignResolution = new Point(320, 180),
        };

    /// <summary>Replaces the attributes of the Window element in the screen's XAML.</summary>
    private void UseWindowAttributes(string attributes)
        => File.WriteAllText(
            Path.Combine(_root, "menu.xaml"),
            Markup.Replace("""Left="0" Top="0" Width="320" Height="200">""", attributes + ">"));

    private DataDrivenScreen BuildOn(
        MGDesktop desktop, HeadlessUiTestHarness.HeadlessRuntime runtime, UIScreenAsset asset, UIScreenActionContext context, params JObject[] commands)
    {
        asset.SourceXamlFile = "menu.xaml";
        foreach (var command in commands)
        {
            asset.UICommands.Add(ToCommand(command));
        }

        var screen = new DataDrivenScreen(asset, _envelopePath, context);
        desktop.Windows.Add(screen.BuildWindow(desktop));
        _activeDesktop = desktop;
        _activeRuntime = runtime;
        return screen;
    }

    private MGDesktop? _activeDesktop;
    private HeadlessUiTestHarness.HeadlessRuntime? _activeRuntime;

    private static MGDesktop NewDesktop(out HeadlessUiTestHarness.HeadlessRuntime runtime)
    {
        (var desktop, runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        return desktop;
    }

    private static MGButton Button(XamlUIScreenBase screen, string name)
    {
        var window = Assert.Single(screen.GetWindows());
        Assert.True(window.TryGetElementByName(name, out MGButton button));
        return button;
    }

    private static Point ButtonCentre(XamlUIScreenBase screen, string name)
    {
        var button = Button(screen, name);
        return button.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, button.LayoutBounds.Center);
    }

    private void Settle(DataDrivenScreen screen) => Settle(screen, _activeDesktop!, _activeRuntime!);

    private void Settle(DataDrivenScreen screen, MGDesktop desktop, HeadlessUiTestHarness.HeadlessRuntime runtime)
    {
        for (int i = 0; i < 4; i++)
        {
            Step(Away, screen, desktop, runtime);
        }
    }

    private void Click(Point point, DataDrivenScreen screen, bool updateScreen = true)
    {
        var desktop = _activeDesktop!;
        var runtime = _activeRuntime!;
        Step(point, screen, desktop, runtime);
        Step(point, screen, desktop, runtime);
        Step(point, screen, desktop, runtime, ButtonState.Pressed);

        _frame++;
        runtime.ApplyFrame(Frame(point, ButtonState.Released));
        desktop.Update();

        if (updateScreen)
        {
            screen.Update(new GameTime());
        }
    }

    private void PressEnter(Point mouse, DataDrivenScreen screen)
    {
        var desktop = _activeDesktop!;
        var runtime = _activeRuntime!;
        Step(mouse, screen, desktop, runtime, ButtonState.Released, new KeyboardState(Keys.Enter));
        Step(mouse, screen, desktop, runtime);
        Step(mouse, screen, desktop, runtime);
    }

    private void Step(Point mouse, DataDrivenScreen screen) => Step(mouse, screen, _activeDesktop!, _activeRuntime!);

    private void Step(
        Point mouse,
        DataDrivenScreen screen,
        MGDesktop desktop,
        HeadlessUiTestHarness.HeadlessRuntime runtime,
        ButtonState left = ButtonState.Released,
        KeyboardState keyboard = default)
    {
        _frame++;
        runtime.ApplyFrame(Frame(mouse, left, keyboard));
        desktop.Update();
        screen.Update(new GameTime());
    }

    private UpdateBaseArgs Frame(Point mouse, ButtonState left, KeyboardState keyboard = default)
        => new(
            TimeSpan.FromMilliseconds(16 * _frame),
            TimeSpan.FromMilliseconds(16),
            new MouseState(mouse.X, mouse.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            keyboard);
}
