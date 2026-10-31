using System.Globalization;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.FontStashSharp;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;

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
    private readonly List<FontHold> _fontHolds = new();
    private readonly MGDesktop _desktop;
    private readonly HeadlessUiTestHarness.HeadlessRuntime _runtime;
    private readonly UIScreenActionContext _context;
    private bool _isTop = true;
    private bool _isFadeRunning;
    private bool _doesAFadeRun;
    private int _frame;
    private Action<IUIScreen> _onClose = static _ => { };
    private Action<string> _onOpen = static _ => { };

    // Set by the tests that need a screen frozen by a real stack: the top of the stack then answers IsTopScreen.
    private ScreenStack? _stack;

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
            IsTopScreen = screen => _stack != null ? ReferenceEquals(_stack.Top, screen) : _isTop,
            OpenScreen = name =>
            {
                _calls.Add($"open:{name}");
                _onOpen(name);
            },
            CloseScreen = screen =>
            {
                _calls.Add("close");
                _onClose(screen);
            },
            ChangeWorld = name => _calls.Add($"world:{name}"),
            PlaySound = sound => _calls.Add($"sound:{sound.Name}"),
            Quit = () => _calls.Add("quit"),
            StartFade = action =>
            {
                _calls.Add(string.Create(CultureInfo.InvariantCulture, $"fade:{action.Layer}:{action.BlendMode}:{action.DurationSeconds}"));
                _isFadeRunning = _isFadeRunning || _doesAFadeRun;
            },
            IsFadeRunning = () => _isFadeRunning,
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
    public void WithoutFocusWrap_TheFocusStopsAtTheEnds()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne" });
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.Up);
        Assert.Equal("btnOne", FocusedName());

        Tap(navigation, screen, Keys.Down);
        Tap(navigation, screen, Keys.Down);
        Tap(navigation, screen, Keys.Down);
        Assert.Equal("btnThree", FocusedName());
    }

    [Fact]
    public void AVerticalWrap_JoinsBothEnds()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne", FocusWrap = UIScreenFocusWrap.Vertical });
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.Up);
        Assert.Equal("btnThree", FocusedName());

        Tap(navigation, screen, Keys.Down);
        Assert.Equal("btnOne", FocusedName());

        Tap(navigation, screen, Keys.Down);
        Assert.Equal("btnTwo", FocusedName());
    }

    [Fact]
    public void AHorizontalWrap_JoinsBothEnds_AndLeavesUpAndDownAlone()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtonsInARow);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne", FocusWrap = UIScreenFocusWrap.Horizontal });
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.Up);
        Assert.Equal("btnOne", FocusedName());

        Tap(navigation, screen, Keys.Left);
        Assert.Equal("btnThree", FocusedName());

        Tap(navigation, screen, Keys.Right);
        Assert.Equal("btnOne", FocusedName());
    }

    [Fact]
    public void AWrap_NeedsNoMapping_AndWorksOnMGUIsOwnPathToo()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne", FocusWrap = UIScreenFocusWrap.Vertical });
        Settle(screen);

        Step(Away, screen, _desktop, _runtime, ButtonState.Released, new KeyboardState(Keys.Up));
        Step(Away, screen);

        Assert.Equal("btnThree", FocusedName());
    }

    [Fact]
    public void AWrap_WaitsForTheFocus_SoTheFirstKeyOfAScreenWithoutADefaultFocusDoesNotWrap()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(new UIScreenAsset { FocusWrap = UIScreenFocusWrap.Vertical });
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);
        Assert.Null(_desktop.FocusedKeyboardHandler);

        Tap(navigation, screen, Keys.Up);
        Assert.Equal("btnOne", FocusedName());

        Tap(navigation, screen, Keys.Up);
        Assert.Equal("btnThree", FocusedName());
    }

    [Fact]
    public void AWrap_TakesTheListOnce_SoADisabledEndStopsIt()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne", FocusWrap = UIScreenFocusWrap.Vertical });
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Button(screen, "btnThree").IsEnabled = false;
        Settle(screen);
        Tap(navigation, screen, Keys.Up);

        Assert.Equal("btnOne", FocusedName());
    }

    [Fact]
    public void AWrap_OnASingleElement_DoesNothing()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="320" Height="200">
              <Button Name="btnOne" Content="One" />
            </Window>
            """);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne", FocusWrap = UIScreenFocusWrap.Vertical });
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.Up);

        Assert.Equal("btnOne", FocusedName());
        Assert.Empty(Button(screen, "btnOne").NavigationNeighbors);
    }

    [Fact]
    public void AnUnknownFocusWrap_FailsAtLoad()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse("""{ "id": "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234", "name": "n", "focus_wrap": "both" }"""));
        asset.SourceXamlFile = "menu.xaml";
        asset.UICommands.Add(ToCommand(PlayCommand(("quit", null))));

        var screen = new DataDrivenScreen(asset, _envelopePath, _context);
        var error = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'focus_wrap'", error.Message);
    }

    [Fact]
    public void Cancel_RunsTheCancelCommand_OnTheFrameOfTheInput()
    {
        var asset = new UIScreenAsset { DefaultFocus = "btnPlay", CancelCommand = "Back" };
        var screen = BuildOn(_desktop, _runtime, asset, Mapped(), PlayCommand(("quit", null)), Named("Back", ("close_screen", null), ("play_sound", "Confirm")));
        var navigation = NavigationOf(_desktop, screen);
        Settle(navigation, screen);

        NavigationStep(navigation, screen, new KeyboardState(Keys.X));

        Assert.Equal(new[] { "close", "sound:Confirm" }, _calls);
    }

    [Fact]
    public void Cancel_OnAScreenWithoutACancelCommand_DoesNothing()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, PlayCommand(("quit", null)));
        var navigation = NavigationOf(_desktop, screen);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.X);

        Assert.Empty(_calls);
    }

    [Fact]
    public void Cancel_OffTheTopOfTheStack_IsDropped_LikeAClick()
    {
        var asset = new UIScreenAsset { CancelCommand = "Back" };
        var screen = BuildOn(_desktop, _runtime, asset, Mapped(), PlayCommand(("quit", null)), Named("Back", ("close_screen", null)));
        Settle(screen);

        _isTop = false;
        screen.ReportCancel();
        _isTop = true;
        screen.Update(new GameTime());

        Assert.Empty(_calls);
    }

    [Fact]
    public void ACancelCommand_ThatNamesNothing_FailsAtLoad()
    {
        var asset = new UIScreenAsset { CancelCommand = "Back" };

        var error = Assert.Throws<InvalidOperationException>(
            () => BuildOn(_desktop, _runtime, asset, Mapped(), PlayCommand(("quit", null))));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'cancel_command' names command 'Back'", error.Message);
    }

    [Fact]
    public void ACancelCommand_InAProjectWithoutAMapping_FailsAtLoad()
    {
        var asset = new UIScreenAsset { CancelCommand = "Back" };

        var error = Assert.Throws<InvalidOperationException>(
            () => Build(asset, PlayCommand(("quit", null)), Named("Back", ("close_screen", null))));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("UIInputMapping", error.Message);
    }

    [Fact]
    public void AMoveCommand_ThatNamesNothing_FailsAtLoad()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset { MoveCommand = "Tick" }, PlayCommand(("quit", null))));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'move_command' names command 'Tick'", error.Message);
    }

    [Fact]
    public void AMoveCommand_NeedsNoMapping_AndRunsOnMGUIsOwnPathToo()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlain", MoveCommand = "Tick" }, PlayCommand(("quit", null)), Named("Tick", ("play_sound", "Confirm")));
        Settle(screen);

        Step(Away, screen, _desktop, _runtime, ButtonState.Released, new KeyboardState(Keys.Down));
        Step(Away, screen);

        Assert.Same(Button(screen, "btnPlay"), _desktop.FocusedKeyboardHandler);
        Assert.Equal(new[] { "sound:Confirm" }, _calls);
    }

    [Fact]
    public void AMoveCommand_RunsOncePerMove_AndAgainOnAWrap()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(
            new UIScreenAsset { DefaultFocus = "btnOne", FocusWrap = UIScreenFocusWrap.Vertical, MoveCommand = "Tick" },
            Named("Tick", ("play_sound", "Confirm")));
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);
        Assert.Empty(_calls);

        Tap(navigation, screen, Keys.Down);
        Assert.Single(_calls);

        Tap(navigation, screen, Keys.Up);
        Tap(navigation, screen, Keys.Up);

        Assert.Equal("btnThree", FocusedName());
        Assert.Equal(3, _calls.Count);
    }

    [Fact]
    public void AMoveCommand_DoesNotRun_WhenTheFocusStops()
    {
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), ThreeButtons);
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnOne", MoveCommand = "Tick" }, Named("Tick", ("play_sound", "Confirm")));
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.Up);

        Assert.Empty(_calls);
    }

    [Fact]
    public void AMoveCommand_DoesNotRun_WhenTheDefaultFocusReplacesWhatMGUIPicked()
    {
        // With a key as the last input and nothing focused, MGUI picks the first button it can. When the target of the
        // default focus is ready at the same time, its request wins and MGUI picks nothing, so it has to come later.
        _runtime.ApplyFrame(Frame(Away, ButtonState.Released));
        _desktop.Update();
        _runtime.ApplyFrame(Frame(Away, ButtonState.Released, new KeyboardState(Keys.Left)));
        _desktop.Update();
        File.WriteAllText(Path.Combine(_root, "menu.xaml"), Markup.Replace("""<Button Name="btnPlay" """, """<Button Name="btnPlay" IsEnabled="False" """));

        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay", MoveCommand = "Tick" }, PlayCommand(("quit", null)), Named("Tick", ("play_sound", "Confirm")));
        var seen = new List<string?>();
        _desktop.FocusedKeyboardHandlerChanged += (_, e) => seen.Add(e.NewValue?.Name);
        Settle(screen);
        Assert.Equal("btnPlain", FocusedName());

        Button(screen, "btnPlay").IsEnabled = true;
        Settle(screen);

        Assert.Equal(new[] { "btnPlain", "btnPlay" }, seen);
        Assert.Empty(_calls);
    }

    [Fact]
    public void AMoveCommand_DoesNotRun_WhenTheFocusArrivesFromAnotherScreen()
    {
        var below = Build(new UIScreenAsset { DefaultFocus = "btnPlain" }, PlayCommand(("quit", null)));
        var navigation = NavigationOf(_desktop);
        Settle(navigation, below);
        Tap(navigation, below, Keys.Left);

        var above = Build(new UIScreenAsset { MoveCommand = "Tick" }, PlayCommand(("quit", null)), Named("Tick", ("play_sound", "Confirm")));
        for (int i = 0; i < 6; i++)
        {
            NavigationStep(navigation, above, default);
        }

        Assert.Same(Assert.Single(above.GetWindows()), _desktop.FocusedKeyboardHandler?.SelfOrParentWindow);
        Assert.Empty(_calls);
    }

    [Fact]
    public void AMoveCommand_DoesNotRun_ForAFocusTheMouseGave()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlain", MoveCommand = "Tick" }, Named("Play", ("quit", null)), Named("Tick", ("play_sound", "Confirm")));
        Settle(screen);

        Button(screen, "btnPlay").Focus(KeyboardFocusSource.Pointer);
        Step(Away, screen);
        Step(Away, screen);

        Assert.Equal(UIInputMode.Pointer, _desktop.ActiveInputMode);
        Assert.Same(Button(screen, "btnPlay"), _desktop.FocusedKeyboardHandler);
        Assert.Empty(_calls);
    }

    [Fact]
    public void ASubmitInTheSameFrameAsAMove_RunsItsCommand_AndTheMoveIsDropped()
    {
        var asset = new UIScreenAsset { DefaultFocus = "btnPlain", MoveCommand = "Tick" };
        var screen = BuildOn(_desktop, _runtime, asset, Mapped(), PlayCommand(("quit", null)), Named("Tick", ("play_sound", "Confirm")));
        var navigation = NavigationOf(_desktop);
        Settle(navigation, screen);

        Tap(navigation, screen, Keys.Down);
        _calls.Clear();

        // Submit comes first in the mapping, so it presses btnPlay, then Up moves away from it.
        NavigationStep(navigation, screen, new KeyboardState(Keys.Z, Keys.Up));

        Assert.Equal(new[] { "quit" }, _calls);
        Assert.Equal("btnPlain", FocusedName());
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
    [InlineData("unknown action", """{ "name": "Play", "actions": [ { "type": "shake_screen", "duration": 12 } ] }""", "shake_screen")]
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
            StartFade = _context.StartFade,
            IsFadeRunning = _context.IsFadeRunning,
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

    // ADR-0081: a list that waits for a fade.

    private static readonly JObject Fade = JObject.Parse("""
        { "type": "fade_screen", "layer": "AboveUI", "blend_mode": "Subtractive", "from": [ 0, 0, 0 ], "to": [ 255, 255, 255 ], "duration_seconds": 0.5 }
        """);

    private static readonly JObject Wait = JObject.Parse("""{ "type": "wait_fade" }""");

    private static JObject CommandOf(params JObject[] actions)
        => new() { ["name"] = "Play", ["actions"] = new JArray(actions) };

    private static JObject Action(string type, string? target = null)
        => target == null ? new JObject { ["type"] = type } : new JObject { ["type"] = type, ["target"] = target };

    private static JObject FadeWith(string field, JToken? value)
    {
        var fade = (JObject)Fade.DeepClone();

        if (value == null)
        {
            fade.Remove(field);
        }
        else
        {
            fade[field] = value;
        }

        return fade;
    }

    /// <summary>A list that plays a sound, fades, waits, then changes world, with the fade running once it started.</summary>
    private DataDrivenScreen BuildFadeOut(UIScreenAsset? asset = null)
    {
        var screen = Build(asset ?? new UIScreenAsset(), CommandOf(Action("play_sound", "Confirm"), Fade, Wait, Action("change_world", "Level1.world")));
        Settle(screen);
        return screen;
    }

    private void RunUntilSuspended(DataDrivenScreen screen)
    {
        _isFadeRunning = true;
        Click(ButtonCentre(screen, "btnPlay"), screen);
        Assert.True(screen.IsCommandSuspended);
    }

    private const string StartedFade = "fade:AboveUI:Subtractive:0.5";

    private static JObject Open(params JObject[] actions)
        => new() { ["name"] = "Open", ["actions"] = new JArray(actions) };

    [Fact]
    public void AnOpenCommand_StartsAtLoad_BeforeAnyUpdate()
    {
        _isFadeRunning = true;

        var screen = Build(new UIScreenAsset { OnOpenCommand = "Open" }, Open(Fade, Wait), CommandOf(Action("quit")));

        Assert.Equal(new[] { StartedFade }, _calls);
        Assert.True(screen.IsCommandSuspended);
    }

    [Fact]
    public void WhileAnOpenCommandWaits_NoOtherCommandRuns_ThenTheScreenWorks()
    {
        _isFadeRunning = true;
        var screen = Build(new UIScreenAsset { OnOpenCommand = "Open" }, Open(Fade, Wait), CommandOf(Action("quit")));
        Settle(screen);
        _calls.Clear();

        Click(ButtonCentre(screen, "btnPlay"), screen);
        Assert.Empty(_calls);

        _isFadeRunning = false;
        Step(Away, screen);
        Assert.False(screen.IsCommandSuspended);

        Click(ButtonCentre(screen, "btnPlay"), screen);
        Assert.Equal(new[] { "quit" }, _calls);
    }

    [Fact]
    public void AnOpenCommand_WithNoWait_EndsAtLoad()
    {
        var screen = Build(new UIScreenAsset { OnOpenCommand = "Open" }, Open(Fade), CommandOf(Action("quit")));

        Assert.Equal(new[] { StartedFade }, _calls);
        Assert.False(screen.IsCommandSuspended);
    }

    [Fact]
    public void NoOpenCommand_RunsNothingAtLoad()
    {
        var screen = Build(new UIScreenAsset(), Open(Fade), CommandOf(Action("quit")));

        Settle(screen);

        Assert.Empty(_calls);
    }

    [Theory]
    [InlineData("quit", null)]
    [InlineData("close_screen", null)]
    [InlineData("change_world", "Level1.world")]
    [InlineData("open_screen", "Options")]
    public void AnOpenCommand_RefusesWhatChangesTheStackOrTheWorld(string type, string? target)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset { OnOpenCommand = "Open" }, Open(Action(type, target))));

        Assert.Contains("on_open_command", exception.Message);
        Assert.Contains(_envelopePath, exception.Message);
        Assert.Contains(type, exception.Message);
    }

    [Fact]
    public void AnOpenCommand_TheEnvelopeDoesNotDeclare_IsRefused()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset { OnOpenCommand = "Missing" }, Open(Fade)));

        Assert.Contains("'on_open_command' names command 'Missing'", exception.Message);
    }

    [Fact]
    public void AFade_ReachesTheGame_AndAWaitWithNoFadeRunning_DoesNotStopTheList()
    {
        var screen = BuildFadeOut();

        Click(ButtonCentre(screen, "btnPlay"), screen);

        Assert.Equal(new[] { "sound:Confirm", StartedFade, "world:Level1.world" }, _calls);
        Assert.False(screen.IsCommandSuspended);
    }

    [Fact]
    public void AList_WaitsAcrossFrames_ThenGoesOnFromTheNextAction()
    {
        var screen = BuildFadeOut();

        RunUntilSuspended(screen);
        Assert.Equal(new[] { "sound:Confirm", StartedFade }, _calls);

        for (int i = 0; i < 5; i++)
        {
            Step(Away, screen);
        }

        Assert.Equal(new[] { "sound:Confirm", StartedFade }, _calls);
        Assert.True(screen.IsCommandSuspended);

        _isFadeRunning = false;
        Step(Away, screen);

        Assert.Equal(new[] { "sound:Confirm", StartedFade, "world:Level1.world" }, _calls);
        Assert.False(screen.IsCommandSuspended);
    }

    [Fact]
    public void AListThatEnded_DoesNotRunAgain_AndACommandCanFollow()
    {
        var screen = BuildFadeOut();
        RunUntilSuspended(screen);
        _isFadeRunning = false;
        Step(Away, screen);
        Step(Away, screen);
        Assert.Equal(3, _calls.Count);

        Click(ButtonCentre(screen, "btnPlay"), screen);

        Assert.Equal(6, _calls.Count);
    }

    [Fact]
    public void AListCanWaitTwice()
    {
        _doesAFadeRun = true;
        var screen = Build(new UIScreenAsset(), CommandOf(Fade, Wait, Fade, Wait, Action("quit")));
        Settle(screen);
        Click(ButtonCentre(screen, "btnPlay"), screen);
        Assert.Equal(new[] { StartedFade }, _calls);

        _isFadeRunning = false;
        Step(Away, screen);
        Assert.Equal(new[] { StartedFade, StartedFade }, _calls);
        Assert.True(screen.IsCommandSuspended);

        Step(Away, screen);
        Assert.Equal(new[] { StartedFade, StartedFade }, _calls);

        _isFadeRunning = false;
        Step(Away, screen);
        Assert.Equal(new[] { StartedFade, StartedFade, "quit" }, _calls);
    }

    [Fact]
    public void ASecondConfirmDuringAWait_IsRefused()
    {
        var screen = BuildFadeOut();
        RunUntilSuspended(screen);

        Click(ButtonCentre(screen, "btnPlay"), screen);
        PressEnter(Away, screen);
        Step(Away, screen);

        Assert.Equal(new[] { "sound:Confirm", StartedFade }, _calls);

        _isFadeRunning = false;
        Step(Away, screen);
        Step(Away, screen);

        Assert.Equal(new[] { "sound:Confirm", StartedFade, "world:Level1.world" }, _calls);
    }

    [Fact]
    public void ACancelDuringAWait_IsRefused()
    {
        var back = Named("Back", ("quit", null));
        var screen = BuildOn(_desktop, _runtime, new UIScreenAsset { CancelCommand = "Back" }, Mapped(), CommandOf(Fade, Wait), back);
        Settle(screen);
        RunUntilSuspended(screen);

        screen.ReportCancel();
        Step(Away, screen);

        Assert.Equal(new[] { StartedFade }, _calls);
    }

    [Fact]
    public void AHideDuringAWait_DisposesAtOnce_AndTheListIsDropped()
    {
        var screen = BuildFadeOut();
        RunUntilSuspended(screen);

        screen.Hide();

        Assert.False(screen.IsCommandSuspended);
        Assert.True(Assert.Single(_soundHandles).IsDisposed);
        Assert.Equal(new[] { "sound:Confirm", StartedFade }, _calls);
    }

    [Fact]
    public void ADisposeDuringAWait_ClearsTheList_AndGivesTheKeyboardBack()
    {
        var screen = BuildFadeOut();
        var stack = StackOf(screen);
        RunUntilSuspended(screen);
        Assert.True(stack.IsNavigationSuspended);

        screen.Dispose();

        Assert.False(screen.IsCommandSuspended);
        Assert.False(stack.IsNavigationSuspended);
    }

    [Fact]
    public void DuringAWait_MGUIsOwnPathStops_AndTheFocusStays()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, CommandOf(Fade, Wait));
        var stack = StackOf(screen);
        var play = Button(screen, "btnPlay");

        _isFadeRunning = true;
        TapRoot(stack, Keys.Enter);
        Assert.True(screen.IsCommandSuspended);
        Assert.True(_desktop.IsNavigationInputSuspended);

        TapRoot(stack, Keys.Up);

        Assert.Same(play, _desktop.FocusedKeyboardHandler);

        _isFadeRunning = false;
        RootStep(stack);
        Assert.False(_desktop.IsNavigationInputSuspended);

        TapRoot(stack, Keys.Up);

        Assert.Same(Button(screen, "btnPlain"), _desktop.FocusedKeyboardHandler);
    }

    /// <summary>The failure this guards: the tracker repeats a held button whoever reads it, and A is Submit.</summary>
    [Fact]
    public void AButtonHeldThroughAWait_RunsNothingWhenItEnds_UntilItIsPressedAgain()
    {
        var screen = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, CommandOf(Fade, Wait));
        var stack = StackOf(screen);
        _isFadeRunning = true;

        RootStep(stack, gamePad: HoldingA);
        Assert.True(screen.IsCommandSuspended);

        RootSteps(stack, FramesPastARepeat, HoldingA);
        _isFadeRunning = false;
        RootSteps(stack, FramesPastARepeat, HoldingA);

        Assert.False(screen.IsCommandSuspended);
        Assert.Equal(new[] { StartedFade }, _calls);

        RootStep(stack);
        RootStep(stack, gamePad: HoldingA);
        RootStep(stack);

        Assert.Equal(new[] { StartedFade, StartedFade }, _calls);
    }

    [Fact]
    public void AButtonHeldThroughAWait_DoesNotConfirmTheScreenItOpens()
    {
        var title = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, CommandOf(Fade, Wait, Action("open_screen", "Options")));
        var stack = StackOf(title);
        _onOpen = _ => stack.PushBuilt(Build(new UIScreenAsset { IsModal = true, DefaultFocus = "btnPlay" }, PlayCommand(("quit", null))));
        _isFadeRunning = true;

        RootStep(stack, gamePad: HoldingA);
        RootSteps(stack, FramesPastARepeat, HoldingA);
        _isFadeRunning = false;
        RootSteps(stack, FramesPastARepeat, HoldingA);

        Assert.Equal(new[] { StartedFade, "open:Options" }, _calls);

        RootStep(stack);
        RootStep(stack, gamePad: HoldingA);
        RootStep(stack);

        Assert.Equal(new[] { StartedFade, "open:Options", "quit" }, _calls);
    }

    /// <summary>A frozen screen runs nothing, so nothing it would do at the end of its wait can give the input back.</summary>
    [Fact]
    public void AModalPushedOverAWait_HasTheKeyboardAndTheGamepad_AndTheListResumesWhenItCloses()
    {
        var title = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, CommandOf(Fade, Wait, Action("quit")));
        var stack = StackOf(title);
        _isFadeRunning = true;
        TapRoot(stack, Keys.Enter);
        Assert.True(_desktop.IsNavigationInputSuspended);

        var modal = Build(new UIScreenAsset { IsModal = true, DefaultFocus = "btnPlay" }, PlayCommand(("play_sound", "Confirm")));
        stack.PushBuilt(modal);
        RootSteps(stack, 4);
        Assert.False(_desktop.IsNavigationInputSuspended);

        TapRoot(stack, Keys.Enter);
        Assert.Equal(new[] { StartedFade, "sound:Confirm" }, _calls);

        _isFadeRunning = false;
        RootSteps(stack, FramesPastARepeat);
        Assert.True(title.IsCommandSuspended);
        Assert.False(_desktop.IsNavigationInputSuspended);

        RootStep(stack, gamePad: HoldingA);
        RootStep(stack);
        Assert.Equal(new[] { StartedFade, "sound:Confirm", "sound:Confirm" }, _calls);

        CloseOn(stack, modal);
        RootStep(stack);

        Assert.False(title.IsCommandSuspended);
        Assert.Equal(new[] { StartedFade, "sound:Confirm", "sound:Confirm", "quit" }, _calls);
    }

    [Fact]
    public void TheStopComesBack_WhenAScreenOverAWaitClosesBeforeTheFadeEnds()
    {
        var title = Build(new UIScreenAsset { DefaultFocus = "btnPlay" }, CommandOf(Fade, Wait));
        var stack = StackOf(title);
        _isFadeRunning = true;
        TapRoot(stack, Keys.Enter);

        var modal = Build(new UIScreenAsset { IsModal = true }, PlayCommand(("quit", null)));
        stack.PushBuilt(modal);
        RootStep(stack);
        Assert.False(_desktop.IsNavigationInputSuspended);

        CloseOn(stack, modal);
        RootStep(stack);

        Assert.True(_desktop.IsNavigationInputSuspended);
    }

    [Fact]
    public void AWaitingScreenThatLeavesTheStack_LeavesTheNavigationFree()
    {
        var screen = Build(new UIScreenAsset(), CommandOf(Fade, Wait));
        var stack = StackOf(screen);
        RunUntilSuspended(screen);
        RootStep(stack);
        Assert.True(_desktop.IsNavigationInputSuspended);

        CloseOn(stack, screen);
        RootStep(stack);

        Assert.False(_desktop.IsNavigationInputSuspended);
    }

    [Fact]
    public void AWait_LeavesTheValueItFound()
    {
        var screen = BuildFadeOut();
        _desktop.UseRawNavigationInput = false;

        RunUntilSuspended(screen);
        Assert.False(_desktop.UseRawNavigationInput);

        _isFadeRunning = false;
        Step(Away, screen);

        Assert.False(_desktop.UseRawNavigationInput);
    }

    [Fact]
    public void DuringAWait_AMappedProjectIgnoresItsKeys_AndTheFocusStays()
    {
        var desktop = NewDesktop(out var runtime);
        var navigation = NavigationOf(desktop);
        var screen = BuildOn(desktop, runtime, new UIScreenAsset { DefaultFocus = "btnPlay" }, Mapped(), CommandOf(Fade, Wait, Action("quit")));
        Settle(navigation, screen);

        _isFadeRunning = true;
        Tap(navigation, screen, Keys.Z);
        Assert.True(screen.IsCommandSuspended);

        // What UIRoot does each frame.
        navigation.IsSuspended = screen.IsCommandSuspended;
        Tap(navigation, screen, Keys.Up);
        Tap(navigation, screen, Keys.Z);
        Assert.Equal("btnPlay", FocusedName());
        Assert.Equal(new[] { StartedFade }, _calls);

        _isFadeRunning = false;
        NavigationStep(navigation, screen, default);
        navigation.IsSuspended = screen.IsCommandSuspended;
        Assert.Equal(new[] { StartedFade, "quit" }, _calls);

        Tap(navigation, screen, Keys.Up);
        Assert.Equal("btnPlain", FocusedName());
    }

    [Fact]
    public void AnActionThatFailsAfterAWait_EndsTheList_AndGivesTheKeyboardBack()
    {
        var failing = new UIScreenActionContext
        {
            ScreenExists = _context.ScreenExists,
            WorldExists = _context.WorldExists,
            AcquireSound = _context.AcquireSound,
            IsTopScreen = _context.IsTopScreen,
            OpenScreen = _context.OpenScreen,
            CloseScreen = _context.CloseScreen,
            ChangeWorld = _context.ChangeWorld,
            PlaySound = _context.PlaySound,
            Quit = () => throw new InvalidOperationException("boom"),
            StartFade = _context.StartFade,
            IsFadeRunning = _context.IsFadeRunning,
        };
        var screen = BuildOn(_desktop, _runtime, new UIScreenAsset(), failing, CommandOf(Fade, Wait, Action("quit")));
        var stack = StackOf(screen);
        RunUntilSuspended(screen);

        _isFadeRunning = false;
        Assert.Throws<InvalidOperationException>(() => Step(Away, screen));

        Assert.False(screen.IsCommandSuspended);
        Assert.False(stack.IsNavigationSuspended);
        Step(Away, screen);
    }

    public static TheoryData<string, JObject, string> RefusedFades => new()
    {
        { "no layer", FadeWith("layer", null), "'layer'" },
        { "no blend mode", FadeWith("blend_mode", null), "'blend_mode'" },
        { "no start", FadeWith("from", null), "'from'" },
        { "no end", FadeWith("to", null), "'to'" },
        { "no duration", FadeWith("duration_seconds", null), "'duration_seconds'" },
        { "a negative duration", FadeWith("duration_seconds", -1f), "'duration_seconds'" },
        { "a duration that is no number", FadeWith("duration_seconds", new JValue(float.NaN)), "'duration_seconds'" },
        { "an endless duration", FadeWith("duration_seconds", new JValue(float.PositiveInfinity)), "'duration_seconds'" },
        { "a target on a fade", FadeWith("target", "Level1.world"), "takes none" },
        { "a target on a wait", Action("wait_fade", "Level1.world"), "takes none" },
    };

    [Theory]
    [MemberData(nameof(RefusedFades))]
    public void AnInvalidFade_IsRefusedAtLoad(string description, JObject action, string expected)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Build(new UIScreenAsset(), CommandOf(action)));

        Assert.True(exception.Message.Contains(expected, StringComparison.Ordinal), description);
        Assert.Contains("menu.uiscreen", exception.Message);
    }

    [Fact]
    public void AMalformedFadeField_IsRefusedAtLoad_ThroughTheFormatErrors()
    {
        var asset = new UIScreenAsset();
        asset.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "menu",
            ["source_xaml_file"] = "menu.xaml",
            ["ui_commands"] = new JArray(CommandOf(FadeWith("layer", "Sideways"))),
        });

        var screen = new DataDrivenScreen(asset, _envelopePath, _context);

        var exception = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));
        Assert.Contains("actions[0].layer", exception.Message);
    }

    [Theory]
    [InlineData("open_screen", "Options")]
    [InlineData("close_screen", null)]
    [InlineData("change_world", "Level1.world")]
    [InlineData("quit", null)]
    public void AWaitAfterAnActionThatEndsTheScreen_IsRefusedAtLoad(string type, string? target)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Build(new UIScreenAsset(), CommandOf(Fade, Action(type, target), Wait)));

        Assert.Contains($"'wait_fade' after '{type}'", exception.Message);
    }

    [Fact]
    public void AnActionAfterAWait_IsLegal_WhateverItDoes()
    {
        var screen = Build(new UIScreenAsset(), CommandOf(
            Action("play_sound", "Confirm"), Fade, Wait, Action("open_screen", "Options"), Action("close_screen"), Action("quit")));

        Assert.NotNull(screen);
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

    [Fact]
    public void TheFonts_AreHeldInOrder_AndGivenBackWhenTheScreenIsHidden()
    {
        var asset = new UIScreenAsset();
        asset.Fonts.Add("first");
        asset.Fonts.Add("second");

        var screen = BuildOn(_desktop, _runtime, asset, HoldingFonts(), PlayCommand(("quit", null)));

        Assert.Equal(new[] { "first", "second" }, _fontHolds.Select(hold => hold.Name));
        Assert.All(_fontHolds, hold => Assert.False(hold.IsDisposed));

        screen.Hide();

        Assert.All(_fontHolds, hold => Assert.True(hold.IsDisposed));
    }

    [Fact]
    public void AFontFamily_IsFoundByTheXaml_BecauseTheFontIsHeldBeforeItLoads()
    {
        const string family = "block-font";
        File.WriteAllText(
            Path.Combine(_root, "menu.xaml"),
            Markup.Replace("""<TextBlock Name="lblTitle" """, $"""<TextBlock Name="lblTitle" FontFamily="{family}" """));

        var ttf = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf"));
        var fontSystem = new FontSystem();
        fontSystem.AddFont(ttf);
        var engine = new FontStashSharpTextEngine();
        engine.AddFontSystem(_runtime.DefaultFontFamily, CustomFontStyles.Normal, fontSystem);
        _desktop.TextEngine = engine;
        var context = ContextWith(null, _ =>
        {
            engine.AddStaticFont(family, CustomFontStyles.Normal, fontSystem.GetFont(8));
            return new FontHold(family);
        });

        var asset = new UIScreenAsset();
        asset.Fonts.Add(family);
        var screen = BuildOn(_desktop, _runtime, asset, context, PlayCommand(("quit", null)));

        Assert.True(Assert.Single(screen.GetWindows()).TryGetElementByName("lblTitle", out MGTextBlock title));
        Assert.Equal(family, title.FontFamily);
    }

    [Fact]
    public void AFontThatCannotBeHeld_FailsAtLoad_NamingTheEnvelopeAndTheFont_AndTheOthersAreGivenBack()
    {
        var context = ContextWith(null, name =>
        {
            if (name == "missing")
            {
                throw new InvalidOperationException("no such font");
            }

            var hold = new FontHold(name);
            _fontHolds.Add(hold);
            return hold;
        });
        var asset = new UIScreenAsset { SourceXamlFile = "menu.xaml" };
        asset.Fonts.Add("first");
        asset.Fonts.Add("missing");
        var screen = new DataDrivenScreen(asset, _envelopePath, context);

        var error = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'missing'", error.Message);
        Assert.Equal("no such font", error.InnerException?.Message);

        // What the caller of a screen that never reached a stack does.
        screen.Dispose();
        Assert.True(Assert.Single(_fontHolds).IsDisposed);
    }

    [Fact]
    public void AFont_WithAHostThatOffersNone_FailsAtLoad()
    {
        var asset = new UIScreenAsset { SourceXamlFile = "menu.xaml" };
        asset.Fonts.Add("first");
        var screen = new DataDrivenScreen(asset, _envelopePath, _context);

        var error = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("'first'", error.Message);
    }

    [Fact]
    public void AMalformedFontList_FailsAtLoad_WithWhatLoadReported()
    {
        var asset = new UIScreenAsset();
        asset.Load(JObject.Parse("""{ "id": "3f2b9e5c-9a1d-4f0b-9c3a-6d7e8f901234", "name": "n", "fonts": [ 3 ] }"""));
        asset.SourceXamlFile = "menu.xaml";
        asset.UICommands.Add(ToCommand(PlayCommand(("quit", null))));

        var screen = new DataDrivenScreen(asset, _envelopePath, _context);
        var error = Assert.Throws<InvalidOperationException>(() => screen.BuildWindow(_desktop));

        Assert.Contains(_envelopePath, error.Message);
        Assert.Contains("fonts[0]", error.Message);
    }

    [Fact]
    public void TheHolds_AreGivenBack_WhenTheXamlCannotLoad()
    {
        File.Delete(Path.Combine(_root, "menu.xaml"));
        var asset = new UIScreenAsset { SourceXamlFile = "menu.xaml" };
        asset.Fonts.Add("first");
        var screen = new DataDrivenScreen(asset, _envelopePath, HoldingFonts());

        Assert.Throws<FileNotFoundException>(() => screen.BuildWindow(_desktop));
        screen.Dispose();

        Assert.True(Assert.Single(_fontHolds).IsDisposed);
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

    private const string ThreeButtons = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Orientation="Vertical" Spacing="8" Padding="16">
            <Button Name="btnOne" Content="One" />
            <Button Name="btnTwo" Content="Two" />
            <Button Name="btnThree" Content="Three" />
          </StackPanel>
        </Window>
        """;

    private const string ThreeButtonsInARow = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Orientation="Horizontal" Spacing="8" Padding="16">
            <Button Name="btnOne" Content="One" />
            <Button Name="btnTwo" Content="Two" />
            <Button Name="btnThree" Content="Three" />
          </StackPanel>
        </Window>
        """;

    private static UINavigationInput NavigationOf(MGDesktop desktop, DataDrivenScreen? cancelTarget = null)
    {
        var mapping = new ButtonsMapping { Name = "ui-mapping" };
        mapping.Buttons.Add(Entry("Submit", Keys.Z));
        mapping.Buttons.Add(Entry("Cancel", Keys.X));
        mapping.Buttons.Add(Entry("NavigateUp", Keys.Up));
        mapping.Buttons.Add(Entry("NavigateDown", Keys.Down));
        mapping.Buttons.Add(Entry("NavigateLeft", Keys.Left));
        mapping.Buttons.Add(Entry("NavigateRight", Keys.Right));

        var navigation = new UINavigationInput(desktop, mapping);
        if (cancelTarget != null)
        {
            navigation.CancelNotHandled = cancelTarget.ReportCancel;
        }

        return navigation;
    }

    private static InputMapping Entry(string name, Keys key)
        => new() { Name = name, KeyButton = new KeyButton(key), AlternativeKeyButton = new KeyButton() };

    private static JObject Named(string name, params (string Type, string? Target)[] actions)
    {
        var command = PlayCommand(actions);
        command["name"] = name;
        return command;
    }

    /// <summary>Presses and releases a key, one frame each, in the order <see cref="UIRoot.Update"/> runs.</summary>
    private void Tap(UINavigationInput navigation, DataDrivenScreen screen, Keys key)
    {
        NavigationStep(navigation, screen, new KeyboardState(key));
        NavigationStep(navigation, screen, default);
    }

    /// <summary>Frames through the navigation input too: its first read of the devices is never an edge.</summary>
    private void Settle(UINavigationInput navigation, DataDrivenScreen screen)
    {
        for (int i = 0; i < 4; i++)
        {
            NavigationStep(navigation, screen, default);
        }
    }
    private void NavigationStep(UINavigationInput navigation, DataDrivenScreen screen, KeyboardState keyboard)
    {
        var desktop = _activeDesktop!;
        _frame++;
        _activeRuntime!.ApplyFrame(Frame(Away, ButtonState.Released, keyboard));
        desktop.Update();
        navigation.Update(TimeSpan.FromMilliseconds(16 * _frame));
        screen.Update(new GameTime());
    }

    private string? FocusedName() => _activeDesktop!.FocusedKeyboardHandler?.Name;

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
    private UIScreenActionContext Scaled() => ContextWith(new Point(320, 180), null);

    /// <summary>The recording context, for a project that names a UI input mapping.</summary>
    private UIScreenActionContext Mapped() => ContextWith(null, null, hasUIInputMapping: true);

    /// <summary>The recording context, with the optional members set.</summary>
    private UIScreenActionContext ContextWith(Point? designResolution, Func<string, IDisposable>? acquireFont, bool hasUIInputMapping = false)
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
            StartFade = _context.StartFade,
            IsFadeRunning = _context.IsFadeRunning,
            DesignResolution = designResolution,
            AcquireFont = acquireFont!,
            HasUIInputMapping = hasUIInputMapping,
        };

    /// <summary>A context that records what it holds in <see cref="_fontHolds"/>, in order.</summary>
    private UIScreenActionContext HoldingFonts()
        => ContextWith(null, name =>
        {
            var hold = new FontHold(name);
            _fontHolds.Add(hold);
            return hold;
        });

    private sealed class FontHold(string name) : IDisposable
    {
        public string Name { get; } = name;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

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

    private const int FramesPastARepeat = 40; // At 16 ms a frame: a held button repeats after 350 ms, then every 120.

    private static readonly GamePadState NoButton = new(Vector2.Zero, Vector2.Zero, 0f, 0f, Array.Empty<Buttons>());
    private static readonly GamePadState HoldingA = new(Vector2.Zero, Vector2.Zero, 0f, 0f, Buttons.A);

    /// <summary>Puts a settled screen on a stack of its own: from here on the stack says who is at the top.</summary>
    private ScreenStack StackOf(DataDrivenScreen screen)
    {
        Settle(screen);
        _stack = new ScreenStack(null);
        _stack.PushBuilt(screen);
        return _stack;
    }

    /// <summary>What <see cref="ScreenStack.Remove"/> does, with no <see cref="UIRoot"/>.</summary>
    private void CloseOn(ScreenStack stack, DataDrivenScreen screen)
    {
        stack.RemoveBuilt(screen);
        screen.Hide();
        _activeDesktop!.Windows.Remove(Assert.Single(screen.GetWindows()));
    }

    /// <summary>One frame of a project with no mapping, in the order of <see cref="UIRoot.Update"/>.</summary>
    private void RootStep(ScreenStack stack, KeyboardState keyboard = default, GamePadState? gamePad = null)
    {
        var desktop = _activeDesktop!;
        var runtime = _activeRuntime!;
        _frame++;
        runtime.Input.GamePad.StateOverride = gamePad ?? NoButton;
        runtime.ApplyFrame(Frame(Away, ButtonState.Released, keyboard));
        desktop.Update();
        stack.Update(new GameTime());
        desktop.IsNavigationInputSuspended = stack.IsNavigationSuspended;
    }

    private void RootSteps(ScreenStack stack, int count, GamePadState? gamePad = null)
    {
        for (int i = 0; i < count; i++)
        {
            RootStep(stack, gamePad: gamePad);
        }
    }

    private void TapRoot(ScreenStack stack, Keys key)
    {
        RootStep(stack, new KeyboardState(key));
        RootStep(stack);
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
