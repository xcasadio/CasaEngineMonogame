using System.Collections;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Cutscenes;
using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace CasaEngine.Tests.Input;

/// <summary>The AnyKey input device: a new press of any key, except Shift, Control, Alt and Windows alone.</summary>
public sealed class AnyKeyInputTests
{
    private const string EntryJson = """
        {
          "id": "5b62b859-83e3-4d9d-9962-a735b8b94119",
          "name": "exit",
          "game_pad_number": "One",
          "button_behavior": "DigitalInput",
          "analog_axis": "MouseX",
          "invert": false,
          "dead_zone": 0.75,
          "key_button": { "key": "None", "game_pad_button": "None", "mouse_button": "LeftButton", "input_device": "AnyKey" },
          "alternative_key_button": { "key": "None", "game_pad_button": "A", "mouse_button": "LeftButton", "input_device": "GamePad" }
        }
        """;

    private static bool Pressed(KeyboardManager keyboard)
        => new KeyButton { InputDevice = InputDevices.AnyKey }.IsPressed(
            keyboard, new MouseManager(), new GamePadManager().GetGamePad(PlayerIndex.One));

    private static KeyboardManager Frames(params Keys[][] frames)
    {
        var keyboard = new KeyboardManager();
        foreach (var frame in frames)
        {
            keyboard.Update(new KeyboardState(frame));
        }

        return keyboard;
    }

    [Fact]
    public void AnyKey_IsAppendedAfterTheExistingDevices()
    {
        Assert.Equal(3, (int)InputDevices.Mouse);
        Assert.Equal(4, (int)InputDevices.AnyKey);
    }

    [Fact]
    public void ANewPress_IsPressed()
    {
        Assert.True(Pressed(Frames([], [Keys.Q])));
    }

    [Fact]
    public void NoKey_IsNotPressed()
    {
        Assert.False(Pressed(Frames([], [])));
    }

    [Fact]
    public void AHeldKey_IsPressedOnlyOnTheFirstFrame()
    {
        Assert.False(Pressed(Frames([], [Keys.Q], [Keys.Q])));
    }

    [Fact]
    public void ANewKeyWhileAnotherIsHeld_IsPressed()
    {
        Assert.True(Pressed(Frames([Keys.Q], [Keys.Q, Keys.W])));
    }

    [Theory]
    [InlineData(Keys.LeftShift)]
    [InlineData(Keys.RightShift)]
    [InlineData(Keys.LeftControl)]
    [InlineData(Keys.RightControl)]
    [InlineData(Keys.LeftAlt)]
    [InlineData(Keys.RightAlt)]
    [InlineData(Keys.LeftWindows)]
    [InlineData(Keys.RightWindows)]
    public void AModifierAlone_IsNotPressed(Keys modifier)
    {
        Assert.False(Pressed(Frames([], [modifier])));
    }

    [Theory]
    [InlineData(Keys.LeftShift)]
    [InlineData(Keys.RightShift)]
    [InlineData(Keys.LeftControl)]
    [InlineData(Keys.RightControl)]
    public void ShiftOrControl_ThenALetter_IsPressed(Keys modifier)
    {
        Assert.True(Pressed(Frames([modifier], [modifier, Keys.A])));
        Assert.True(Pressed(Frames([], [modifier, Keys.A])));
    }

    [Theory]
    [InlineData(Keys.LeftAlt)]
    [InlineData(Keys.RightAlt)]
    [InlineData(Keys.LeftWindows)]
    [InlineData(Keys.RightWindows)]
    public void AltOrWindowsHeld_ThenAKey_IsNotPressed(Keys modifier)
    {
        Assert.False(Pressed(Frames([modifier], [modifier, Keys.Tab])));
        Assert.False(Pressed(Frames([modifier], [modifier, Keys.D])));
    }

    [Fact]
    public void TheEntryOfTheMappingFormat_Loads()
    {
        var mapping = new InputMapping();

        mapping.Load(JObject.Parse(EntryJson));

        Assert.Equal(InputDevices.AnyKey, mapping.KeyButton.InputDevice);
        Assert.Equal(Keys.None, mapping.KeyButton.Key);
        Assert.Equal(InputDevices.GamePad, mapping.AlternativeKeyButton.InputDevice);
    }

    [Fact]
    public void AKeyButtonWithoutKey_StillFailsToLoad()
    {
        var button = new KeyButton();
        var node = JObject.Parse("{ \"game_pad_button\": \"None\", \"mouse_button\": \"LeftButton\", \"input_device\": \"AnyKey\" }");

        Assert.ThrowsAny<Exception>(() => button.Load(node));
    }

    [Fact]
    public void AMappingEntry_FollowsTheFrames()
    {
        var keyboard = new KeyboardManager();
        var mappings = new InputMappingManager();
        var entry = NewEntry();
        mappings.AddInputMapping(entry);
        var mouse = new MouseManager();
        var pads = new GamePadManager();

        keyboard.Update(new KeyboardState());
        mappings.Update(keyboard, mouse, pads);
        Assert.False(entry.Pressed);

        keyboard.Update(new KeyboardState(Keys.Q));
        mappings.Update(keyboard, mouse, pads);
        Assert.True(entry.Pressed);
        Assert.False(entry.PressedPreviousFrame);

        keyboard.Update(new KeyboardState(Keys.Q));
        mappings.Update(keyboard, mouse, pads);
        Assert.False(entry.Pressed);
    }

    [Fact]
    public void TheRead_DoesNotAllocate()
    {
        var keyboard = Frames([], [Keys.LeftShift, Keys.Q]);
        Assert.True(keyboard.IsAnyKeyJustPressed());

        var before = AllocationWindow.Start();
        var count = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (keyboard.IsAnyKeyJustPressed())
            {
                count++;
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1000, count);
    }

    // ---- ExitOnInput ----

    [Fact]
    public void ExitOnInput_WithAnAnyKeyEntry_ExitsOnANewPress()
    {
        var rig = new WatchRig();
        rig.Frames(3, []);
        Assert.False(rig.Exited);

        rig.Frames(1, [Keys.F5]);
        rig.Frames(3, [Keys.F5]);

        Assert.True(rig.Exited);
    }

    [Fact]
    public void ExitOnInput_WithAnAnyKeyEntry_IgnoresModifiersAndAltTab()
    {
        var rig = new WatchRig();
        rig.Frames(3, []);

        rig.Frames(5, [Keys.LeftShift]);
        rig.Frames(1, []);
        rig.Frames(1, [Keys.LeftAlt]);
        rig.Frames(5, [Keys.LeftAlt, Keys.Tab]);

        Assert.False(rig.Exited);
    }

    [Fact]
    public void ExitOnInput_WithAnAnyKeyEntry_IgnoresAKeyHeldAtTheArming()
    {
        var rig = new WatchRig();
        rig.Keyboard.Update(new KeyboardState(Keys.Space));

        rig.Frames(10, [Keys.Space]);
        Assert.False(rig.Exited);

        rig.Frames(1, []);
        rig.Frames(4, [Keys.Space]);
        Assert.True(rig.Exited);
    }

    private static InputMapping NewEntry() => new()
    {
        Name = "exit",
        ButtonBehavior = ButtonBehaviors.DigitalInput,
        KeyButton = new KeyButton { InputDevice = InputDevices.AnyKey },
        AlternativeKeyButton = new KeyButton(),
    };

    /// <summary>A watch over one AnyKey entry; a frame steps the watch, then updates the keyboard as the game does.</summary>
    private sealed class WatchRig
    {
        private readonly World _world = new();
        private readonly IEnumerator _watch;
        private bool _ended;

        public WatchRig()
        {
            var next = new CutsceneAsset { Name = "next", RootAction = new SequenceCutsceneActionData() };
            _watch = new CutsceneInputWatch(
                _world.CutsceneDirector, [NewEntry()], Keyboard, new MouseManager(), new GamePadManager(), next).Run();
        }

        public KeyboardManager Keyboard { get; } = new();

        public bool Exited => _ended && _world.CutsceneDirector.GetDebugSnapshot().AssetName == "next";

        public void Frames(int count, Keys[] keys)
        {
            for (int i = 0; i < count; i++)
            {
                if (!_ended && !_watch.MoveNext())
                {
                    _ended = true;
                }

                Keyboard.Update(new KeyboardState(keys));
            }
        }
    }
}
