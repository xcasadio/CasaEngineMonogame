using CasaEngine.Engine.Input;
using CasaEngine.Framework.Input;
using MGUI.Core.UI;
using MGUI.Shared.Input.Keyboard;
using MGUI.Shared.Input.Semantic;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Reads a desktop's navigation from a <see cref="ButtonsMapping"/> instead of MGUI's fixed tables (ADR-0080). It
/// turns MGUI's own reading off and sends the desktop an <see cref="InputAction"/> on each input edge.
/// <para/>
/// An entry of the mapping is a UI action when its name is that of a UI <see cref="InputAction"/>. Other entries are
/// not looked at, so a mapping can serve gameplay as well.
/// <para/>
/// <b>Repeat:</b> a direction moves when it is pressed, then repeats. There is one timer for all of them, started
/// again whenever the set of held inputs changes. Every other action fires on its edge only.
/// <para/>
/// <b>Why the states are injected:</b> repeat has to be proved on a gamepad, and a test has none. See
/// <see cref="Update(TimeSpan, Func{KeyButton, bool})"/>.
/// </summary>
public sealed class UINavigationInput
{
    /// <summary>The delay before a held direction repeats when the mapping does not say, in milliseconds.</summary>
    public const int DefaultRepeatInitialDelayMs = 350;

    /// <summary>The time between two repeats when the mapping does not say, in milliseconds.</summary>
    public const int DefaultRepeatIntervalMs = 120;

    // What the mapping may leave unbound and still have work, through InputActionMapper.
    private static readonly Keys[] FallbackKeys = { Keys.Tab, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown };

    private static readonly InputAction[] RequiredActions =
    {
        InputAction.Submit,
        InputAction.Cancel,
        InputAction.NavigateUp,
        InputAction.NavigateDown,
        InputAction.NavigateLeft,
        InputAction.NavigateRight,
    };

    private static readonly KeyButton LeftShift = new(Keys.LeftShift);
    private static readonly KeyButton RightShift = new(Keys.RightShift);

    private const int InputsPerSlot = 2;
    private const ulong SlotInputsMask = 0b11;

    private readonly MGDesktop _desktop;
    private readonly Slot[] _slots;
    private readonly TimeSpan _repeatInitialDelay;
    private readonly TimeSpan _repeatInterval;

    // Kept so that reading the devices allocates no delegate per frame.
    private readonly Func<KeyButton, bool> _isHeldOnDevices;

    // Two bits per slot, one per input. A slot exists per UI action at most, and the fallback keys are five, so this
    // stays well under 64 bits. A set bit in a mask is an input, not an action: the timer speaks of inputs.
    private ulong _heldInputs;
    private ulong _repeatingInputs;

    // Held when this object first read the devices, or while it was suspended: an input in here counts once it has
    // been released.
    private ulong _ignoredInputs;
    private bool _hasReadDevices;

    private TimeSpan _heldSince;
    private TimeSpan? _lastRepeatAt;

    /// <summary>Builds the navigation of <paramref name="desktop"/> from <paramref name="mapping"/>.</summary>
    /// <exception cref="InvalidOperationException">The mapping is refused. The message names it and the problem.</exception>
    public UINavigationInput(MGDesktop desktop, ButtonsMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(mapping);

        CheckRepeat(mapping);

        _desktop = desktop;
        _slots = BuildSlots(desktop, mapping);
        _repeatInitialDelay = TimeSpan.FromMilliseconds(mapping.RepeatInitialDelayMs ?? DefaultRepeatInitialDelayMs);
        _repeatInterval = TimeSpan.FromMilliseconds(mapping.RepeatIntervalMs ?? DefaultRepeatIntervalMs);
        _isHeldOnDevices = IsHeldOnDevices;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Repeats)
            {
                _repeatingInputs |= SlotInputsMask << (i * InputsPerSlot);
            }
        }

        desktop.UseRawNavigationInput = false;
    }

    /// <summary>
    /// Called with no argument when <see cref="InputAction.Cancel"/> went through the desktop and nothing consumed it,
    /// such as an open combo box. What it means is up to the owner.
    /// </summary>
    public Action CancelNotHandled { get; set; }

    /// <summary>
    /// While true, nothing goes from here to the desktop, so a focus that exists stays where it is (ADR-0081). The
    /// desktop's own auto-focus does not stop: with nothing focused, a key or a button gives the focus. The devices are
    /// still read: an input held here counts as one held when this object was built, so it fires nothing once this
    /// turns false, until it is released.
    /// </summary>
    public bool IsSuspended { get; set; }

    /// <summary>Reads the devices through the desktop's input tracker, and acts on what changed.</summary>
    /// <param name="now">The total elapsed time of the frame.</param>
    public void Update(TimeSpan now) => Update(now, _isHeldOnDevices);

    /// <summary>The same, with the inputs read through <paramref name="isHeld"/>.</summary>
    /// <param name="now">The total elapsed time of the frame.</param>
    /// <param name="isHeld">Whether an input is held. A caller on a frame path passes a delegate it keeps.</param>
    internal void Update(TimeSpan now, Func<KeyButton, bool> isHeld)
    {
        ulong held = ReadHeldInputs(isHeld);

        if (!_hasReadDevices || IsSuspended)
        {
            _hasReadDevices = true;
            _ignoredInputs |= held;
        }

        _ignoredInputs &= held;
        held &= ~_ignoredInputs;

        ulong previous = _heldInputs;

        if (held != previous)
        {
            _heldInputs = held;
            _heldSince = now;
            _lastRepeatAt = null;
            FireEdges(held, previous, now, isHeld);
            return;
        }

        if ((held & _repeatingInputs) != 0
            && KeyboardTracker.IsRepeatDue(now, _heldSince, _lastRepeatAt, _repeatInitialDelay, _repeatInterval))
        {
            _lastRepeatAt = now;
            FireRepeats(held, now, isHeld);
        }
    }

    private ulong ReadHeldInputs(Func<KeyButton, bool> isHeld)
    {
        ulong held = 0;

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            int shift = i * InputsPerSlot;

            if (slot.First != null && isHeld(slot.First))
            {
                held |= 1UL << shift;
            }

            if (slot.Second != null && isHeld(slot.Second))
            {
                held |= 2UL << shift;
            }
        }

        return held;
    }

    private void FireEdges(ulong held, ulong previous, TimeSpan now, Func<KeyButton, bool> isHeld)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            int shift = i * InputsPerSlot;
            ulong inputs = (held >> shift) & SlotInputsMask;

            if (inputs != 0 && ((previous >> shift) & SlotInputsMask) == 0)
            {
                Fire(_slots[i], inputs, isRepeat: false, now, isHeld);
            }
        }
    }

    private void FireRepeats(ulong held, TimeSpan now, Func<KeyButton, bool> isHeld)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            ulong inputs = (held >> (i * InputsPerSlot)) & SlotInputsMask;

            if (_slots[i].Repeats && inputs != 0)
            {
                Fire(_slots[i], inputs, isRepeat: true, now, isHeld);
            }
        }
    }

    private void Fire(Slot slot, ulong inputs, bool isRepeat, TimeSpan now, Func<KeyButton, bool> isHeld)
    {
        var input = (inputs & 1) != 0 ? slot.First : slot.Second;
        Keys? key = input.InputDevice == InputDevices.Keyboard ? input.Key : null;

        if (key is { } typed && IsTextForAFocusedTextBox(typed))
        {
            return;
        }

        var action = slot.Action;
        if (slot.FollowsShift)
        {
            InputActionMapper.TryMapKeyboardAction(input.Key, isHeld(LeftShift) || isHeld(RightShift), out action);
        }

        var context = new InputActionContext(
            key.HasValue ? InputActionSource.Keyboard : InputActionSource.GamePad,
            isRepeat ? InputActionPhase.Repeated : InputActionPhase.Pressed,
            now,
            isRepeat,
            key);

        bool isHandled = _desktop.TryHandleInputAction(new InputActionEvent(action, context));

        if (!isHandled && action == InputAction.Cancel && !IsKeptByAFocusedTextBox(key))
        {
            CancelNotHandled?.Invoke();
        }
    }

    /// <summary>
    /// MGUI protects the keys it knows from a text box, and a mapping can bind any key: without this, a letter bound
    /// to Cancel would cancel while the player types it.
    /// </summary>
    private bool IsTextForAFocusedTextBox(Keys key)
        => _desktop.FocusedKeyboardHandler is ITextEntryHost { IsReadonly: false }
           && !InputActionMapper.TryMapKeyboardAction(key, false, out _);

    /// <summary>
    /// MGUI drops a key the focused text box reserves before any control sees the action, and answers false as it does
    /// for a Cancel nothing consumed. That key was never a Cancel: a space bound to it is still a space.
    /// </summary>
    private bool IsKeptByAFocusedTextBox(Keys? key)
        => key is { } typed
           && _desktop.FocusedKeyboardHandler is ITextEntryHost textBox
           && textBox.ShouldPreserveTextEntryKey(typed);

    private bool IsHeldOnDevices(KeyButton input)
        => input.InputDevice switch
        {
            InputDevices.Keyboard => _desktop.InputTracker.Keyboard.CurrentState.IsKeyDown(input.Key),
            InputDevices.GamePad => _desktop.InputTracker.GamePad.CurrentState.IsButtonDown(input.GamePadButton),
            _ => false,
        };

    private static Slot[] BuildSlots(MGDesktop desktop, ButtonsMapping mapping)
    {
        var slots = new List<Slot>(RequiredActions.Length + FallbackKeys.Length);

        // Apart from the slots: an entry that binds nothing builds none, and it still is the one a name resolves to.
        var declared = new HashSet<InputAction>();

        foreach (var entry in mapping.Buttons)
        {
            if (!TryGetUIAction(entry.Name, out var action))
            {
                continue;
            }

            if (!declared.Add(action))
            {
                throw Refused(mapping, $"UI action '{action}' is declared twice.");
            }

            if (entry.ButtonBehavior == ButtonBehaviors.AnalogInput)
            {
                throw Refused(mapping, $"UI action '{action}' is an analog input.");
            }

            var first = ReadInput(desktop, mapping, entry, action, entry.KeyButton);
            var second = ReadInput(desktop, mapping, entry, action, entry.AlternativeKeyButton);

            if (first != null || second != null)
            {
                slots.Add(new Slot(action, first ?? second, first != null ? second : null, IsDirection(action), false));
            }
        }

        foreach (var action in RequiredActions)
        {
            if (!slots.Exists(slot => slot.Action == action))
            {
                throw Refused(mapping, $"UI action '{action}' is required and binds no input.");
            }
        }

        foreach (var key in FallbackKeys)
        {
            if (!slots.Exists(slot => slot.Binds(key)))
            {
                InputActionMapper.TryMapKeyboardAction(key, false, out var action);
                slots.Add(new Slot(action, new KeyButton(key), null, false, key == Keys.Tab));
            }
        }

        return slots.ToArray();
    }

    /// <summary>An exact name of a UI action: a number parses as an enum too, and <c>Pause</c> is not for the UI.</summary>
    private static bool TryGetUIAction(string name, out InputAction action)
    {
        action = default;
        return !string.IsNullOrEmpty(name)
               && Enum.IsDefined(typeof(InputAction), name)
               && Enum.TryParse(name, out action)
               && action.IsUIAction();
    }

    /// <summary>The input, or null when it binds nothing.</summary>
    private static KeyButton ReadInput(MGDesktop desktop, ButtonsMapping mapping, InputMapping entry, InputAction action, KeyButton input)
    {
        switch (input?.InputDevice)
        {
            case InputDevices.Keyboard:
                return input.Key != Keys.None ? input : null;

            case InputDevices.GamePad:
                if (input.GamePadButton == 0)
                {
                    return null;
                }

                if (entry.GamePadNumber != desktop.InputTracker.GamePad.PlayerIndex)
                {
                    throw Refused(
                        mapping,
                        $"UI action '{action}' reads gamepad {entry.GamePadNumber}, but the UI reads gamepad {desktop.InputTracker.GamePad.PlayerIndex}.");
                }

                return input;

            case InputDevices.Mouse:
                throw Refused(mapping, $"UI action '{action}' binds a mouse button, which the UI does not read as an action.");

            default:
                return null;
        }
    }

    private static bool IsDirection(InputAction action)
        => action is InputAction.NavigateUp or InputAction.NavigateDown or InputAction.NavigateLeft or InputAction.NavigateRight;

    private static void CheckRepeat(ButtonsMapping mapping)
    {
        if (mapping.RepeatInitialDelayMs < 0)
        {
            throw Refused(mapping, $"'repeat_initial_delay_ms' is {mapping.RepeatInitialDelayMs}, and cannot be negative.");
        }

        if (mapping.RepeatIntervalMs <= 0)
        {
            throw Refused(mapping, $"'repeat_interval_ms' is {mapping.RepeatIntervalMs}, and must be positive.");
        }
    }

    private static InvalidOperationException Refused(ButtonsMapping mapping, string problem)
        => new($"UI input mapping '{(string.IsNullOrEmpty(mapping.FileName) ? mapping.Name : mapping.FileName)}': {problem}");

    /// <summary>One action and the inputs that give it. <see cref="First"/> is never null.</summary>
    private sealed class Slot
    {
        public Slot(InputAction action, KeyButton first, KeyButton second, bool repeats, bool followsShift)
        {
            Action = action;
            First = first;
            Second = second;
            Repeats = repeats;
            FollowsShift = followsShift;
        }

        public InputAction Action { get; }

        public KeyButton First { get; }

        public KeyButton Second { get; }

        public bool Repeats { get; }

        /// <summary>True when the mapper decides the action from Shift, which is the case of a fallback Tab.</summary>
        public bool FollowsShift { get; }

        public bool Binds(Keys key)
            => IsKey(First, key) || IsKey(Second, key);

        private static bool IsKey(KeyButton input, Keys key)
            => input is { InputDevice: InputDevices.Keyboard } && input.Key == key;
    }
}
