using System.Collections.Generic;

namespace CasaEngine.Demos;

/// <summary>
/// One line of a demo's controls, as the main screen sheet and the in-demo help panel show it (ADR-0090).
/// <para/>
/// A line made with <see cref="ForActions"/> names actions of the demos' input configuration
/// (<c>Content/demos-input.buttonsMapping</c>): the sheet then shows the key and the gamepad button that file binds
/// to them, so the sheet cannot disagree with what the demo reads. A line made with the constructor is free text, for
/// an input that is not an action (the mouse gestures of the camera).
/// </summary>
/// <param name="Keyboard">The key or mouse gesture, as the player reads it ("Right mouse drag").</param>
/// <param name="Effect">What the input does, in a few words.</param>
/// <param name="GamePad">The gamepad button for the same action; empty when the action has none.</param>
public readonly record struct DemoControl(string Keyboard, string Effect, string GamePad = "")
{
    /// <summary>The actions this line shows the bindings of; null for a free-text line.</summary>
    public IReadOnlyList<string>? Actions { get; init; }

    /// <summary>A line whose keys and buttons are read from the input configuration.</summary>
    /// <param name="effect">What the actions do, in a few words.</param>
    /// <param name="actions">One action, or several that read as one line (the four directions).</param>
    public static DemoControl ForActions(string effect, params string[] actions)
    {
        return new DemoControl(string.Empty, effect) { Actions = actions };
    }
}
