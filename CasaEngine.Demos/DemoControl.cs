namespace CasaEngine.Demos;

/// <summary>
/// One line of a demo's controls, as the main screen sheet and the in-demo help panel show it (ADR-0090).
/// </summary>
/// <param name="Keyboard">The key or mouse gesture, as the player reads it ("Space", "Right mouse drag").</param>
/// <param name="Effect">What the input does, in a few words.</param>
/// <param name="GamePad">The gamepad button for the same action; empty when the action has none.</param>
public readonly record struct DemoControl(string Keyboard, string Effect, string GamePad = "");
