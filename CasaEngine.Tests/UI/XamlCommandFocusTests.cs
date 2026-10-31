using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Pins the MGUI behaviour <c>DataDrivenScreen</c> stands on (ADR-0078), with no CasaEngine code in the way: a
/// named command reached by click and by Enter, and when a programmatic focus survives. If one of these changes
/// with an MGUI update, the design of that screen has to be looked at again.
/// </summary>
public class XamlCommandFocusTests
{
    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="200">
          <StackPanel Orientation="Vertical" Spacing="8" Padding="16">
            <Button Name="btnGo" CommandName="Go" Content="Go" />
            <Button Name="btnOther" Content="Other" />
          </StackPanel>
        </Window>
        """;

    private static readonly Point Away = new(-100, -100);

    private readonly MGDesktop _desktop;
    private readonly HeadlessUiTestHarness.HeadlessRuntime _runtime;
    private readonly MGWindow _window;
    private readonly MGButton _button;
    private int _frame;
    private int _commandCount;

    public XamlCommandFocusTests()
    {
        (_desktop, _runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        _window = UIScreenLoader.Load(_desktop, XamlDocumentSource.FromString(Markup, "command-focus.xaml"));
        _window.GetResources().AddCommand("Go", _ => _commandCount++);
        Assert.True(_window.TryGetElementByName("btnGo", out _button));
    }

    [Fact]
    public void AClick_RunsTheCommandNamedByTheButton()
    {
        _desktop.Windows.Add(_window);
        Settle();

        Click(ButtonCentre());

        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void Enter_OnTheFocusedButton_RunsTheSameCommand()
    {
        FocusOnceTheButtonCanReceiveIt(Away);

        PressEnter(Away);

        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void TheFirstEnter_WithNothingFocusedOrHovered_AutoFocusesTheFirstButton_AndRunsItsCommand()
    {
        _desktop.Windows.Add(_window);
        Settle();
        Assert.Null(_desktop.FocusedKeyboardHandler);

        PressEnter(Away);

        Assert.Same(_button, _desktop.FocusedKeyboardHandler);
        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void TheFirstEnter_UnderAHoveredButton_WithNothingFocused_RunsNoCommand()
    {
        // A hovered button stops the auto-focus, and Submit has no fallback: this is why a screen declares its
        // default focus.
        _desktop.Windows.Add(_window);
        Settle();
        var centre = ButtonCentre();
        Step(centre);
        Step(centre);

        PressEnter(centre);

        Assert.Equal(0, _commandCount);
    }

    [Fact]
    public void Enter_UnderAHoveredButton_ThatHasTheFocus_RunsTheCommand()
    {
        FocusOnceTheButtonCanReceiveIt(Away);
        var centre = ButtonCentre();
        Step(centre);
        Step(centre);

        PressEnter(centre);

        Assert.Same(_button, _desktop.FocusedKeyboardHandler);
        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void AFocus_AskedBeforeTheFirstUpdate_IsLost_AndTheQueueIsEmptied()
    {
        _button.Focus(KeyboardFocusSource.Programmatic);
        _desktop.Windows.Add(_window);

        for (int i = 0; i < 5; i++)
        {
            Step(Away);
            Assert.Null(_desktop.FocusedKeyboardHandler);
            Assert.Null(_desktop.QueuedFocusedKeyboardHandler);
        }
    }

    [Fact]
    public void AFocus_AskedAfterASingleUpdate_IsLost()
    {
        _desktop.Windows.Add(_window);
        Step(Away);

        _button.Focus(KeyboardFocusSource.Programmatic);
        Step(Away);
        Step(Away);

        Assert.Null(_desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AFocus_AskedAsSoonAsTheButtonCanReceiveKeyboardInput_AppliesOnTheNextFrame()
    {
        _desktop.Windows.Add(_window);

        int framesBeforeReady = 0;
        while (!CanReceiveKeyboardInput())
        {
            Step(Away);
            Assert.True(++framesBeforeReady <= 5, "The button never became able to receive keyboard input.");
        }

        _button.Focus(KeyboardFocusSource.Programmatic);
        Assert.Null(_desktop.FocusedKeyboardHandler);

        Step(Away);

        Assert.Same(_button, _desktop.FocusedKeyboardHandler);
    }

    private void FocusOnceTheButtonCanReceiveIt(Point mouse)
    {
        _desktop.Windows.Add(_window);

        for (int i = 0; i < 5 && !CanReceiveKeyboardInput(); i++)
        {
            Step(mouse);
        }

        _button.Focus(KeyboardFocusSource.Programmatic);
        Step(mouse);
        Assert.Same(_button, _desktop.FocusedKeyboardHandler);
    }

    private bool CanReceiveKeyboardInput() => ((IKeyboardHandlerHost)_button).CanReceiveKeyboardInput();

    private void Settle()
    {
        for (int i = 0; i < 4; i++)
        {
            Step(Away);
        }
    }

    private Point ButtonCentre()
        => _button.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, _button.LayoutBounds.Center);

    private void Click(Point point)
    {
        Step(point);
        Step(point);
        Step(point, ButtonState.Pressed);
        Step(point);
        Step(point);
    }

    private void PressEnter(Point mouse)
    {
        Step(mouse, ButtonState.Released, new KeyboardState(Keys.Enter));
        Step(mouse);
        Step(mouse);
    }

    private void Step(Point mouse, ButtonState left = ButtonState.Released, KeyboardState keyboard = default)
    {
        _frame++;
        _runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(16 * _frame),
            TimeSpan.FromMilliseconds(16),
            new MouseState(mouse.X, mouse.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            keyboard));
        _desktop.Update();
    }
}
