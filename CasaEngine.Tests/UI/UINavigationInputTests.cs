using CasaEngine.Engine.Input;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;
using XnaButtonState = Microsoft.Xna.Framework.Input.ButtonState;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The navigation a project reads from a <see cref="ButtonsMapping"/> (ADR-0080), on a headless desktop. The button
/// states are injected: a test cannot give MGUI's gamepad tracker a state.
/// </summary>
public sealed class UINavigationInputTests
{
    private const string Markup = """
        <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                Left="0" Top="0" Width="320" Height="400">
          <StackPanel Name="panel" Orientation="Vertical" Spacing="8" Padding="16">
            <Button Name="btnA" CommandName="Go" Content="A" />
            <Button Name="btnB" Content="B" />
            <Button Name="btnC" Content="C" />
            <Button Name="btnD" Content="D" />
            <Button Name="btnE" Content="E" />
            <Button Name="btnF" Content="F" />
            <Button Name="btnG" Content="G" />
            <Button Name="btnH" Content="H" />
            <Button Name="btnI" Content="I" />
            <Button Name="btnJ" Content="J" />
          </StackPanel>
        </Window>
        """;

    private readonly MGDesktop _desktop;
    private readonly HeadlessUiTestHarness.HeadlessRuntime _runtime;
    private readonly MGWindow _window;
    private readonly HashSet<Keys> _keys = new();
    private readonly HashSet<Buttons> _pad = new();
    private readonly Func<KeyButton, bool> _isHeld;
    private int _nowMs;
    private int _commandCount;
    private int _cancelCount;

    public UINavigationInputTests()
    {
        (_desktop, _runtime) = HeadlessUiTestHarness.NewDesktop(1024, 768);
        _window = UIScreenLoader.Load(_desktop, XamlDocumentSource.FromString(Markup, "navigation.xaml"));
        _window.GetResources().AddCommand("Go", _ => _commandCount++);
        _desktop.Windows.Add(_window);
        _isHeld = IsHeld;
    }

    [Fact]
    public void Building_TurnsOffMGUIsOwnReading()
    {
        Assert.True(_desktop.UseRawNavigationInput);

        _ = new UINavigationInput(_desktop, Mapping());

        Assert.False(_desktop.UseRawNavigationInput);
    }

    [Fact]
    public void ADirection_MovesTheFocusWhenPressed_AndOnlyOnceForAShortPress()
    {
        var navigation = Ready(Mapping());

        _keys.Add(Keys.Down);
        Step(navigation);
        Assert.Equal("btnB", Focused());

        Step(navigation, 100);
        _keys.Clear();
        Step(navigation);

        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void AHeldDirection_RepeatsAfterTheDelay_ThenAtTheInterval()
    {
        var navigation = Ready(Mapping(initialDelayMs: 100, intervalMs: 50));

        _pad.Add(Buttons.DPadDown);
        Step(navigation);
        Assert.Equal("btnB", Focused());

        Step(navigation, 99);
        Assert.Equal("btnB", Focused());

        Step(navigation, 1);
        Assert.Equal("btnC", Focused());

        Step(navigation, 49);
        Assert.Equal("btnC", Focused());

        Step(navigation, 1);
        Assert.Equal("btnD", Focused());

        Step(navigation, 50);
        Assert.Equal("btnE", Focused());
    }

    [Fact]
    public void WithoutTheFields_TheDelaysAre350And120()
    {
        var navigation = Ready(Mapping());

        _pad.Add(Buttons.DPadDown);
        Step(navigation);
        Step(navigation, 349);
        Assert.Equal("btnB", Focused());

        Step(navigation, 1);
        Assert.Equal("btnC", Focused());

        Step(navigation, 119);
        Assert.Equal("btnC", Focused());

        Step(navigation, 1);
        Assert.Equal("btnD", Focused());
    }

    [Fact]
    public void ReleasingADirection_StopsTheRepeat_AndThePressAfterIt_StartsTheDelayAgain()
    {
        var navigation = Ready(Mapping(initialDelayMs: 100, intervalMs: 50));

        _keys.Add(Keys.Down);
        Step(navigation);
        Step(navigation, 100);
        Assert.Equal("btnC", Focused());

        _keys.Clear();
        Step(navigation, 10);
        Step(navigation, 500);
        Assert.Equal("btnC", Focused());

        _keys.Add(Keys.Down);
        Step(navigation, 10);
        Assert.Equal("btnD", Focused());

        Step(navigation, 99);
        Assert.Equal("btnD", Focused());
    }

    [Fact]
    public void AnotherInputPressedDuringAHold_StartsTheDelayAgain()
    {
        var navigation = Ready(Mapping(initialDelayMs: 100, intervalMs: 50));

        _pad.Add(Buttons.DPadDown);
        Step(navigation);
        Step(navigation, 90);

        _pad.Add(Buttons.A);
        Step(navigation, 5);
        Assert.Equal("btnB", Focused());

        Step(navigation, 99);
        Assert.Equal("btnB", Focused());

        Step(navigation, 1);
        Assert.Equal("btnC", Focused());
    }

    [Fact]
    public void AnInputReleasedDuringAHold_StartsTheDelayAgain()
    {
        var navigation = Ready(Mapping(initialDelayMs: 100, intervalMs: 50));

        _pad.Add(Buttons.A);
        _pad.Add(Buttons.DPadDown);
        Step(navigation);
        Step(navigation, 90);

        _pad.Remove(Buttons.A);
        Step(navigation, 5);
        Step(navigation, 99);
        Assert.Equal("btnB", Focused());

        Step(navigation, 1);
        Assert.Equal("btnC", Focused());
    }

    [Fact]
    public void TheSecondInputOfAHeldAction_MovesNothing_AndStartsTheDelayAgain()
    {
        var navigation = Ready(Mapping(initialDelayMs: 100, intervalMs: 50));

        _keys.Add(Keys.Down);
        Step(navigation);
        Step(navigation, 90);

        _pad.Add(Buttons.DPadDown);
        Step(navigation, 5);
        Assert.Equal("btnB", Focused());

        Step(navigation, 99);
        Assert.Equal("btnB", Focused());

        Step(navigation, 1);
        Assert.Equal("btnC", Focused());
    }

    [Fact]
    public void BothInputsOfAnAction_PressedTogether_MoveOnce()
    {
        var navigation = Ready(Mapping());

        _keys.Add(Keys.Down);
        _pad.Add(Buttons.DPadDown);
        Step(navigation);

        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void AHeldSubmit_FiresOnItsEdgeOnly()
    {
        var navigation = Ready(Mapping(initialDelayMs: 10, intervalMs: 10));

        _pad.Add(Buttons.A);
        Step(navigation);
        Assert.Equal(1, _commandCount);

        for (int i = 0; i < 20; i++)
        {
            Step(navigation, 50);
        }

        Assert.Equal(1, _commandCount);

        _pad.Clear();
        Step(navigation);
        _pad.Add(Buttons.A);
        Step(navigation);

        Assert.Equal(2, _commandCount);
    }

    [Fact]
    public void AHeldSubmit_DoesNotRepeat_WhileAHeldDirectionDoes()
    {
        var navigation = Ready(Mapping(initialDelayMs: 100, intervalMs: 50));

        // Up from the first button goes nowhere, so the focus stays on the button whose command would run again.
        _pad.Add(Buttons.A);
        Step(navigation);
        _pad.Add(Buttons.DPadUp);
        Step(navigation);

        Step(navigation, 100);
        Step(navigation, 50);

        Assert.Equal("btnA", Focused());
        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void AHeldCancel_FiresOnItsEdgeOnly()
    {
        var navigation = Ready(Mapping(initialDelayMs: 10, intervalMs: 10));

        _pad.Add(Buttons.B);
        Step(navigation);

        for (int i = 0; i < 20; i++)
        {
            Step(navigation, 50);
        }

        Assert.Equal(1, _cancelCount);
    }

    [Fact]
    public void AnEntryNamedAfterAnActionThatDoesNotRepeat_FiresOnItsEdgeOnly()
    {
        var mapping = Mapping(initialDelayMs: 10, intervalMs: 10);
        mapping.Buttons.Add(Entry("NavigateNext", new KeyButton(Keys.Q)));
        var navigation = Ready(mapping);

        _keys.Add(Keys.Q);
        Step(navigation);
        Assert.Equal("btnB", Focused());

        for (int i = 0; i < 5; i++)
        {
            Step(navigation, 50);
        }

        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void ACancelAControlConsumes_IsNotReported()
    {
        var navigation = Ready(Mapping());
        Assert.True(_window.TryGetElementByName("panel", out MGStackPanel panel));
        var combo = new MGComboBox<string>(_window);
        combo.SetItemsSource(new List<string> { "One", "Two" });
        Assert.True(panel.TryAddChild(combo));
        Settle(navigation);
        combo.Focus();
        Step(navigation);
        Assert.Same(combo, _desktop.FocusedKeyboardHandler);
        combo.IsDropdownOpen = true;
        Step(navigation);

        _keys.Add(Keys.X);
        Step(navigation);

        Assert.False(combo.IsDropdownOpen);
        Assert.Equal(0, _cancelCount);

        _keys.Clear();
        Step(navigation);
        _keys.Add(Keys.X);
        Step(navigation);

        Assert.Equal(1, _cancelCount);
    }

    [Fact]
    public void Suspended_NothingReachesTheDesktop_AndTheFocusStays()
    {
        var navigation = Ready(Mapping());
        navigation.CancelNotHandled = () => _cancelCount++;
        navigation.IsSuspended = true;

        foreach (var key in new[] { Keys.Down, Keys.Z, Keys.X, Keys.Tab })
        {
            _keys.Add(key);
            Step(navigation);
            _keys.Clear();
            Step(navigation);
        }

        Assert.Equal("btnA", Focused());
        Assert.Equal(0, _commandCount);
        Assert.Equal(0, _cancelCount);
    }

    [Fact]
    public void Suspended_TheGamepadReachesNothing_Either()
    {
        var navigation = Ready(Mapping());
        navigation.IsSuspended = true;

        foreach (var button in new[] { Buttons.DPadDown, Buttons.A })
        {
            _pad.Add(button);
            Step(navigation);
            _pad.Clear();
            Step(navigation);
        }

        Assert.Equal("btnA", Focused());
        Assert.Equal(0, _commandCount);
    }

    [Fact]
    public void AnInputHeldThroughASuspension_NeitherFiresNorRepeats_UntilItIsPressedAgain()
    {
        var navigation = Ready(Mapping(initialDelayMs: 10, intervalMs: 10));

        navigation.IsSuspended = true;
        _keys.Add(Keys.Down);
        Step(navigation);

        navigation.IsSuspended = false;
        Step(navigation, 50);
        Step(navigation, 50);
        Assert.Equal("btnA", Focused());

        _keys.Clear();
        Step(navigation);
        _keys.Add(Keys.Down);
        Step(navigation);

        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void ADirectionHeldWhenTheSuspensionStarts_StopsRepeating_AndStaysIgnoredAfterIt()
    {
        var navigation = Ready(Mapping(initialDelayMs: 10, intervalMs: 10));

        _keys.Add(Keys.Down);
        Step(navigation);
        Assert.Equal("btnB", Focused());

        navigation.IsSuspended = true;
        Step(navigation, 50);
        Step(navigation, 50);
        navigation.IsSuspended = false;
        Step(navigation, 50);
        Step(navigation, 50);

        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void AnInputPressedDuringASuspension_AndReleased_LeavesNothingBehind()
    {
        var navigation = Ready(Mapping());

        navigation.IsSuspended = true;
        _keys.Add(Keys.Down);
        Step(navigation);
        _keys.Clear();
        Step(navigation);
        navigation.IsSuspended = false;

        Step(navigation);
        Assert.Equal("btnA", Focused());

        _keys.Add(Keys.Down);
        Step(navigation);
        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void TheMappedKeys_ReplaceMGUIsOwn()
    {
        var navigation = Ready(Mapping());

        _keys.Add(Keys.Enter);
        Step(navigation, 16, throughDevices: true);
        _keys.Clear();
        Step(navigation, 16, throughDevices: true);
        Assert.Equal(0, _commandCount);

        _keys.Add(Keys.Z);
        Step(navigation, 16, throughDevices: true);

        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void AKeyTheMappingDoesNotBind_FallsBackOnTheExistingMapper()
    {
        var navigation = Ready(Mapping());

        _keys.Add(Keys.Tab);
        Step(navigation);
        Assert.Equal("btnB", Focused());

        _keys.Clear();
        Step(navigation);

        _keys.Add(Keys.LeftShift);
        Step(navigation);
        _keys.Add(Keys.Tab);
        Step(navigation);

        Assert.Equal("btnA", Focused());
    }

    [Fact]
    public void AFallbackKey_DoesNotRepeat()
    {
        var navigation = Ready(Mapping(initialDelayMs: 10, intervalMs: 10));

        _keys.Add(Keys.Tab);
        Step(navigation);

        for (int i = 0; i < 5; i++)
        {
            Step(navigation, 50);
        }

        Assert.Equal("btnB", Focused());
    }

    [Fact]
    public void AFallbackKeyTheMappingBinds_FollowsTheMapping()
    {
        var mapping = Mapping();
        mapping.Buttons[0] = Entry("Submit", new KeyButton(Keys.Tab));
        var navigation = Ready(mapping);

        _keys.Add(Keys.Tab);
        Step(navigation);

        Assert.Equal("btnA", Focused());
        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void AKeyHeldWhenTheInputIsBuilt_IsNotAnEdge_AndDoesNotRepeat_UntilItIsPressedAgain()
    {
        var mapping = Mapping(initialDelayMs: 10, intervalMs: 10);
        Settle(null);
        _desktop.Windows[0].TryGetElementByName("btnA", out MGButton first);
        first.Focus();
        Settle(null);

        _keys.Add(Keys.Z);
        _keys.Add(Keys.Down);
        var navigation = new UINavigationInput(_desktop, mapping);

        for (int i = 0; i < 5; i++)
        {
            Step(navigation, 50);
        }

        Assert.Equal(0, _commandCount);
        Assert.Equal("btnA", Focused());

        _keys.Clear();
        Step(navigation);
        _keys.Add(Keys.Z);
        Step(navigation);

        Assert.Equal(1, _commandCount);
    }

    [Fact]
    public void ALetterBoundToAnAction_StaysTextInATextBox_AndAnEscapeBoundToCancelDoesNot()
    {
        var mapping = Mapping();
        mapping.Buttons[1] = Entry("Cancel", new KeyButton(Keys.X), new KeyButton(Keys.Escape));
        var navigation = Ready(mapping);
        Assert.True(_window.TryGetElementByName("panel", out MGStackPanel panel));
        var textBox = new MGTextBox(_window);
        Assert.True(panel.TryAddChild(textBox));
        Settle(navigation);
        textBox.Focus();
        Step(navigation);
        Assert.Same(textBox, _desktop.FocusedKeyboardHandler);

        _keys.Add(Keys.X);
        Step(navigation);
        Assert.Equal(0, _cancelCount);

        _keys.Clear();
        Step(navigation);
        _keys.Add(Keys.Down);
        Step(navigation);
        Assert.Same(textBox, _desktop.FocusedKeyboardHandler);

        _keys.Clear();
        Step(navigation);
        _keys.Add(Keys.Escape);
        Step(navigation);
        Assert.Equal(1, _cancelCount);
    }

    [Fact]
    public void ALetterBoundToAnAction_StillWorks_InAReadOnlyTextBox()
    {
        var navigation = Ready(Mapping());
        Assert.True(_window.TryGetElementByName("panel", out MGStackPanel panel));
        var textBox = new MGTextBox(_window) { IsReadonly = true, IsFocusable = true };
        Assert.True(panel.TryAddChild(textBox));
        Settle(navigation);
        textBox.Focus();
        Step(navigation);
        Assert.Same(textBox, _desktop.FocusedKeyboardHandler);

        _keys.Add(Keys.X);
        Step(navigation);

        Assert.Equal(1, _cancelCount);
    }

    [Theory]
    [InlineData("Submit")]
    [InlineData("Cancel")]
    [InlineData("NavigateUp")]
    [InlineData("NavigateDown")]
    [InlineData("NavigateLeft")]
    [InlineData("NavigateRight")]
    public void ARequiredAction_ThatIsMissing_IsRefused(string action)
    {
        var mapping = Mapping();
        mapping.Buttons.RemoveAll(entry => entry.Name == action);

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains(mapping.Name, error.Message);
        Assert.Contains($"'{action}'", error.Message);
        Assert.True(_desktop.UseRawNavigationInput);
    }

    [Theory]
    [InlineData(InputDevices.NoDevice)]
    [InlineData(InputDevices.Keyboard)]
    [InlineData(InputDevices.GamePad)]
    public void ARequiredAction_ThatBindsNothing_IsRefused(InputDevices device)
    {
        var mapping = Mapping();
        mapping.Buttons[0] = Entry("Submit", new KeyButton { InputDevice = device });

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains("'Submit'", error.Message);
    }

    [Fact]
    public void AnAnalogInputOnAUIAction_IsRefused()
    {
        var mapping = Mapping();
        mapping.Buttons[2].ButtonBehavior = ButtonBehaviors.AnalogInput;

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains("analog", error.Message);
        Assert.Contains($"'{mapping.Buttons[2].Name}'", error.Message);
    }

    [Fact]
    public void AnOptionalUIAction_FollowsTheSameRules()
    {
        var mapping = Mapping();
        mapping.Buttons.Add(Entry("NavigateHome", new KeyButton(Keys.Q)));
        mapping.Buttons[^1].ButtonBehavior = ButtonBehaviors.AnalogInput;

        Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));
    }

    [Fact]
    public void AUIActionDeclaredTwice_IsRefused()
    {
        var mapping = Mapping();
        mapping.Buttons.Add(Entry("Submit", new KeyButton(Keys.Q)));

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains("'Submit' is declared twice", error.Message);
    }

    [Fact]
    public void AMouseButtonOnAUIAction_IsRefused()
    {
        var mapping = Mapping();
        mapping.Buttons[0] = Entry("Submit", new KeyButton(Keys.Z), new KeyButton(MouseButtons.LeftButton));

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains("mouse", error.Message);
    }

    [Fact]
    public void AnotherGamepadOnAUIAction_IsRefused()
    {
        var mapping = Mapping();
        mapping.Buttons[0].GamePadNumber = PlayerIndex.Two;

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains("gamepad Two", error.Message);
    }

    [Fact]
    public void AnEntryThatIsNotAUIAction_IsIgnored_WhateverItDeclares()
    {
        var mapping = Mapping();
        mapping.Buttons.Add(Entry("Pause", new KeyButton(MouseButtons.LeftButton)));
        mapping.Buttons.Add(Entry("GameplayPrimary", new KeyButton(Keys.Z)));
        mapping.Buttons.Add(Entry("MoveUp", new KeyButton(Keys.W)));
        mapping.Buttons.Add(Entry("0", new KeyButton(Keys.Q)));
        mapping.Buttons.Add(Entry("submit", new KeyButton(Keys.Q)));
        mapping.Buttons[^1].ButtonBehavior = ButtonBehaviors.AnalogInput;
        mapping.Buttons[^2].ButtonBehavior = ButtonBehaviors.AnalogInput;
        mapping.Buttons[^3].ButtonBehavior = ButtonBehaviors.AnalogInput;
        var navigation = Ready(mapping);

        _keys.Add(Keys.W);
        _keys.Add(Keys.Q);
        Step(navigation);

        Assert.Equal("btnA", Focused());
        Assert.Equal(0, _commandCount);
    }

    [Theory]
    [InlineData(-1, null, "repeat_initial_delay_ms")]
    [InlineData(null, 0, "repeat_interval_ms")]
    [InlineData(null, -5, "repeat_interval_ms")]
    public void ARepeatOutOfRange_IsRefused(int? initialDelayMs, int? intervalMs, string field)
    {
        var mapping = Mapping(initialDelayMs, intervalMs);

        var error = Assert.Throws<InvalidOperationException>(() => new UINavigationInput(_desktop, mapping));

        Assert.Contains(field, error.Message);
        Assert.Contains(mapping.Name, error.Message);
    }

    [Fact]
    public void ADelayOfZero_RepeatsOnTheNextFrame_NotOnTheFrameOfThePress()
    {
        var navigation = Ready(Mapping(initialDelayMs: 0, intervalMs: 50));

        _keys.Add(Keys.Down);
        Step(navigation);
        Assert.Equal("btnB", Focused());

        Step(navigation);
        Assert.Equal("btnC", Focused());
    }

    [Fact]
    public void AFrameWithNothingNew_AllocatesNothing()
    {
        var navigation = Ready(Mapping());
        Step(navigation, 16, throughDevices: true);
        var now = TimeSpan.FromMilliseconds(_nowMs);

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            navigation.Update(now);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private UINavigationInput Ready(ButtonsMapping mapping)
    {
        var navigation = new UINavigationInput(_desktop, mapping)
        {
            CancelNotHandled = () => _cancelCount++,
        };

        Settle(navigation);
        Assert.True(_window.TryGetElementByName("btnA", out MGButton first));
        first.Focus();
        Settle(navigation);
        Assert.Equal("btnA", Focused());
        return navigation;
    }

    private void Settle(UINavigationInput? navigation)
    {
        for (int i = 0; i < 4; i++)
        {
            if (navigation != null)
            {
                Step(navigation);
            }
            else
            {
                Frame(16);
            }
        }
    }

    /// <summary>One frame, <paramref name="elapsedMs"/> after the previous one.</summary>
    private void Step(UINavigationInput navigation, int elapsedMs = 16, bool throughDevices = false)
    {
        Frame(elapsedMs);

        if (throughDevices)
        {
            navigation.Update(TimeSpan.FromMilliseconds(_nowMs));
        }
        else
        {
            navigation.Update(TimeSpan.FromMilliseconds(_nowMs), _isHeld);
        }
    }

    private void Frame(int elapsedMs)
    {
        _nowMs += elapsedMs;
        _runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(_nowMs),
            TimeSpan.FromMilliseconds(elapsedMs),
            new MouseState(-100, -100, 0, XnaButtonState.Released, XnaButtonState.Released, XnaButtonState.Released, XnaButtonState.Released, XnaButtonState.Released),
            new KeyboardState(_keys.ToArray())));
        _desktop.Update();
    }

    private bool IsHeld(KeyButton input)
        => input.InputDevice == InputDevices.Keyboard ? _keys.Contains(input.Key) : _pad.Contains(input.GamePadButton);

    private string? Focused() => _desktop.FocusedKeyboardHandler?.Name;

    private static ButtonsMapping Mapping(int? initialDelayMs = null, int? intervalMs = null)
    {
        var mapping = new ButtonsMapping
        {
            Name = "ui-mapping",
            RepeatInitialDelayMs = initialDelayMs,
            RepeatIntervalMs = intervalMs,
        };

        mapping.Buttons.Add(Entry("Submit", new KeyButton(Keys.Z), new KeyButton(Buttons.A)));
        mapping.Buttons.Add(Entry("Cancel", new KeyButton(Keys.X), new KeyButton(Buttons.B)));
        mapping.Buttons.Add(Entry("NavigateUp", new KeyButton(Keys.Up), new KeyButton(Buttons.DPadUp)));
        mapping.Buttons.Add(Entry("NavigateDown", new KeyButton(Keys.Down), new KeyButton(Buttons.DPadDown)));
        mapping.Buttons.Add(Entry("NavigateLeft", new KeyButton(Keys.Left), new KeyButton(Buttons.DPadLeft)));
        mapping.Buttons.Add(Entry("NavigateRight", new KeyButton(Keys.Right), new KeyButton(Buttons.DPadRight)));
        return mapping;
    }

    private static InputMapping Entry(string name, KeyButton first, KeyButton? second = null)
        => new() { Name = name, KeyButton = first, AlternativeKeyButton = second ?? new KeyButton() };
}
