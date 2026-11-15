using CasaEngine.Demos;
using CasaEngine.Demos.Demos;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.Cameras;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Input;

/// <summary>
/// The keyboard bindings of the input mappings and of the camera navigation are key positions on a US QWERTY keyboard,
/// read on the active layout (ADR-0096); the scripts' direct key reads stay by letter (decision D6). Every test runs on a
/// simulated AZERTY layout.
/// </summary>
[Collection(KeyboardLayoutCollection.Name)]
public sealed class KeyPositionBindingTests : IDisposable
{
    public KeyPositionBindingTests()
    {
        KeyboardLayout.UseLayout(KeyboardLayoutCollection.Azerty);
    }

    public void Dispose()
    {
        TestKeyboardLayout.UseQwerty();
    }

    [Fact]
    public void AButtonBoundToW_IsPressedByZ()
    {
        var keyboard = new KeyboardManager();
        var button = new KeyButton(Keys.W);

        keyboard.Update(new KeyboardState(Keys.Z));
        Assert.True(button.IsPressed(keyboard, new MouseManager(), null!));

        keyboard.Update(new KeyboardState(Keys.W));
        Assert.False(button.IsPressed(keyboard, new MouseManager(), null!));

        Assert.Equal(Keys.W, button.Key);
    }

    [Fact]
    public void AnAxisBoundToAAndD_ReadsQAndD()
    {
        var keyboard = new KeyboardManager();
        var axis = new Axis
        {
            AxisBehavior = Axis.AxisBehaviors.DigitalInput,
            NegativeKeyButton = new KeyButton(Keys.A),
            PositiveKeyButton = new KeyButton(Keys.D),
            AlternativeNegativeKeyButton = new KeyButton(),
            AlternativePositiveKeyButton = new KeyButton(),
        };

        keyboard.Update(new KeyboardState(Keys.Q));
        axis.Update(keyboard, new MouseManager(), null!, 0.016f);
        Assert.Equal(-1f, axis.ValueRaw);

        keyboard.Update(new KeyboardState(Keys.A));
        axis.Update(keyboard, new MouseManager(), null!, 0.016f);
        Assert.Equal(0f, axis.ValueRaw);
    }

    [Fact]
    public void AScriptReadingAKeyDirectly_StillReadsTheLetter()
    {
        var keyboard = new KeyboardManager();
        var playerInput = new PlayerInput(
            new PlayerController { IsInputEnable = true },
            keyboard,
            new MouseManager(),
            new GamePadManager(),
            new InputMappingManager(),
            () => null);

        keyboard.Update(new KeyboardState(Keys.W));

        Assert.True(playerInput.IsKeyPressed(Keys.W));
    }

    [Fact]
    public void TheCameraFliesForwardOnZ()
    {
        var forward = ViewportCameraNavigation.ReadKeys(new KeyboardState(Keys.Z), new KeyboardState(), includeLetterKeys: true);
        var onW = ViewportCameraNavigation.ReadKeys(new KeyboardState(Keys.W), new KeyboardState(), includeLetterKeys: true);

        Assert.True((forward & ViewportNavigationKeys.Forward) != 0);
        Assert.True((onW & ViewportNavigationKeys.Forward) == 0);
    }

    [Fact]
    public void AMappingLoadedAndSaved_KeepsItsKeys()
    {
        var source = JObject.Parse("""
            {
              "id": "6c1f1a2e-0000-4000-8000-000000000001",
              "name": "positions",
              "buttons": [
                {
                  "id": "6c1f1a2e-0000-4000-8000-000000000002",
                  "name": "Forward",
                  "game_pad_number": "One",
                  "button_behavior": "DigitalInput",
                  "analog_axis": "MouseX",
                  "invert": false,
                  "dead_zone": 0.75,
                  "key_button": { "key": "W", "game_pad_button": "None", "mouse_button": "LeftButton", "input_device": "Keyboard" },
                  "alternative_key_button": { "key": "None", "game_pad_button": "None", "mouse_button": "LeftButton", "input_device": "NoDevice" }
                }
              ]
            }
            """);
        var mapping = new ButtonsMapping();
        mapping.Load(source);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(mapping, out JObject saved));

        Assert.Equal("W", (string?)saved.SelectToken("$..key_button.key"));
    }

    [Fact]
    public void TheAnimationDemoHuds_NameTheKeysOfTheLayout()
    {
        var blend = AnimationHudText.ForBlendDemo(LabelOnLayout);
        var ik = AnimationHudText.ForIkDemo(LabelOnLayout);

        Assert.StartsWith("[Z] Walk  [Shift+Z] Run", blend.WalkAndReset);
        Assert.StartsWith("[A]/[E] ", blend.LayerWeight);
        Assert.StartsWith("[Z/Q/D] ", blend.Explore);
        Assert.StartsWith("[1] Idle  [2] Walk  [3] Run", blend.Clips);
        Assert.Contains("[I] Toggle IK", ik.Toggles);
        Assert.StartsWith("[O]/[P] ", ik.Weight);
    }

    [Fact]
    public void TheAudioDemoHud_NamesTheKeysOfTheLayout()
    {
        var audio = AudioHudText.Create(LabelOnLayout);

        Assert.Equal("A", audio.LoopPoint);
        Assert.Contains(audio.HelpLines, line => line.StartsWith("A loop point", StringComparison.Ordinal));
        Assert.Contains(audio.HelpLines, line => line.Contains("Comma mute Master", StringComparison.Ordinal));
    }

    // The label the demos show for an action of their input configuration: the key the layout puts at the bound position.
    private static string LabelOnLayout(string action)
    {
        foreach (var button in DemosMapping.Value["buttons"]!)
        {
            if ((string?)button["name"] == action)
            {
                var key = Enum.Parse<Keys>((string)button["key_button"]!["key"]!);
                return DemoBindingText.KeyLabel(KeyboardLayout.FromQwertyPosition(key));
            }
        }

        throw new InvalidOperationException($"'{action}' is not in demos-input.buttonsMapping.");
    }

    private static readonly Lazy<JObject> DemosMapping = new(() =>
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
        {
            directory = directory.Parent;
        }

        string path = Path.Combine(directory!.FullName, "CasaEngine.Demos", "Content", "demos-input.buttonsMapping");
        return JObject.Parse(File.ReadAllText(path));
    });
}
