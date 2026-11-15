using System;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Input;
using Microsoft.Xna.Framework.Input;
using InputDevices = CasaEngine.Engine.Input.InputDevices;

namespace CasaEngine.Demos;

/// <summary>
/// The input of the demos app: every demo, the main screen and the help panel read named actions here, never a key or
/// a button. The actions and what they are bound to (one key, plus a second key or a gamepad button) are the engine's
/// own input configuration: the asset <c>Content/demos-input.buttonsMapping</c>, registered in the game's
/// <see cref="InputMappingManager"/>, which the engine updates once per frame. To change a key or a button, edit that
/// file: the demos and their sheets follow.
/// <para/>
/// Nothing is read while the game window is inactive. No allocation per call.
/// </summary>
internal static class DemoInput
{
    public const string MappingFileName = "demos-input.buttonsMapping";

    // Actions of the app itself and the four directions many demos share.
    public const string Back = "App.Back";
    public const string Help = "App.Help";
    public const string Launch = "App.Launch";
    public const string MoveLeft = "Move.Left";
    public const string MoveRight = "Move.Right";
    public const string MoveUp = "Move.Up";
    public const string MoveDown = "Move.Down";

    /// <summary>Loads the configuration and hands its actions to the engine. Called once, when the game loads.</summary>
    public static void Load(CasaEngineGame game)
    {
        AssetInfo asset = AssetCatalog.GetByFileName(MappingFileName)
            ?? throw new InvalidOperationException($"Demo input: '{MappingFileName}' is missing from Content/AssetInfos.json.");

        ButtonsMapping mapping = game.AssetContentManager.LoadCopy<ButtonsMapping>(asset.Id);
        InputMappingManager manager = game.InputComponent.InputMappingManager;
        for (int i = 0; i < mapping.Buttons.Count; i++)
        {
            if (manager.Contains(mapping.Buttons[i].Name))
            {
                throw new InvalidOperationException($"Demo input: the action '{mapping.Buttons[i].Name}' is declared twice in '{MappingFileName}'.");
            }

            manager.AddInputMapping(mapping.Buttons[i]);
        }
    }

    /// <summary>True while the key or the button of <paramref name="action"/> is held.</summary>
    public static bool IsDown(CasaEngineGame game, string action)
    {
        if (game == null || !game.IsActive)
        {
            return false;
        }

        return Get(game, action).Pressed;
    }

    /// <summary>
    /// True while the gamepad button of <paramref name="action"/> is held; its key is not looked at. For a reader that
    /// must tell the two devices apart: gameplay that may use the gamepad while a UI holds the keyboard.
    /// </summary>
    public static bool IsButtonDown(CasaEngineGame game, string action)
    {
        if (game == null || !game.IsActive)
        {
            return false;
        }

        InputMapping mapping = Get(game, action);
        return mapping.AlternativeKeyButton.InputDevice == InputDevices.GamePad
            && game.InputComponent.GamePadManager.GetGamePad(mapping.GamePadNumber).ButtonPressed(mapping.AlternativeKeyButton.GamePadButton);
    }

    /// <summary>True on the frame the key or the button of <paramref name="action"/> goes down.</summary>
    public static bool WasPressed(CasaEngineGame game, string action)
    {
        if (game == null || !game.IsActive)
        {
            return false;
        }

        InputMapping mapping = Get(game, action);
        return mapping.Pressed && !mapping.PressedPreviousFrame;
    }

    /// <summary>
    /// True when the key or the button the configuration binds to <paramref name="action"/> is down in the device states
    /// given. For the app's own actions only (back to the menu, help, launch): they read the devices themselves because
    /// the engine hands the keyboard to a modal screen alone while one is open, and leaving a demo must still work then.
    /// </summary>
    public static bool IsBoundDown(CasaEngineGame game, string action, in KeyboardState keyboard, in GamePadState gamePad)
    {
        DemoBinding binding = Describe(game, action);
        return (binding.Key != Keys.None && keyboard.IsKeyDown(binding.Key))
            || (binding.AlternativeKey != Keys.None && keyboard.IsKeyDown(binding.AlternativeKey))
            || (binding.Button != 0 && gamePad.IsButtonDown(binding.Button));
    }

    /// <summary>What <paramref name="action"/> is bound to, for the sheets.</summary>
    public static DemoBinding Describe(CasaEngineGame game, string action)
    {
        InputMapping mapping = Get(game, action);

        Keys key = mapping.KeyButton.InputDevice == InputDevices.Keyboard ? mapping.KeyButton.Key : Keys.None;
        Keys alternativeKey = mapping.AlternativeKeyButton.InputDevice == InputDevices.Keyboard ? mapping.AlternativeKeyButton.Key : Keys.None;
        Buttons button = mapping.AlternativeKeyButton.InputDevice == InputDevices.GamePad ? mapping.AlternativeKeyButton.GamePadButton : 0;
        return new DemoBinding(key, alternativeKey, button);
    }

    // An unknown name is a mistake in a demo or in the file: fail at once, with the name.
    private static InputMapping Get(CasaEngineGame game, string action)
    {
        if (game.InputComponent.InputMappingManager.TryGet(action, out InputMapping? mapping) && mapping != null)
        {
            return mapping;
        }

        throw new InvalidOperationException($"Demo input: no action named '{action}' in '{MappingFileName}'.");
    }
}
