using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.Demos;

/// <summary>
/// Turns a demo's sheet data into the plain text the main screen and the in-demo help panel display (ADR-0090). Called
/// when a screen is built, never per frame.
/// </summary>
internal static class DemoSheetText
{
    /// <summary>One line per control: "Space / A: restart the cutscene"; the gamepad part is left out when empty.</summary>
    /// <param name="controls">The lines of the sheet.</param>
    /// <param name="resolveAction">
    /// Gives the binding of an action of the input configuration; needed as soon as a line names actions
    /// (<see cref="DemoControl.ForActions"/>).
    /// </param>
    public static string FormatControls(IReadOnlyList<DemoControl> controls, Func<string, DemoBinding>? resolveAction = null)
    {
        if (controls == null || controls.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        for (int i = 0; i < controls.Count; i++)
        {
            if (i > 0)
            {
                text.Append('\n');
            }

            string keyboard = controls[i].Keyboard;
            string gamePad = controls[i].GamePad;
            IReadOnlyList<string>? actions = controls[i].Actions;
            if (actions != null)
            {
                if (resolveAction == null)
                {
                    throw new ArgumentNullException(nameof(resolveAction), $"The control '{controls[i].Effect}' names actions: their bindings must be resolved.");
                }

                var bindings = new DemoBinding[actions.Count];
                for (int action = 0; action < actions.Count; action++)
                {
                    bindings[action] = resolveAction(actions[action]);
                }

                keyboard = DemoBindingText.KeyboardText(bindings);
                gamePad = DemoBindingText.GamePadText(bindings);
            }

            text.Append(keyboard);
            if (!string.IsNullOrEmpty(gamePad))
            {
                // An action bound to a button only (no key) shows the button alone.
                if (!string.IsNullOrEmpty(keyboard))
                {
                    text.Append(" / ");
                }

                text.Append(gamePad);
            }

            text.Append(": ").Append(controls[i].Effect);
        }

        return text.ToString();
    }

    /// <summary>One line per entry, each starting with "- ".</summary>
    public static string FormatHighlights(IReadOnlyList<string> highlights)
    {
        if (highlights == null || highlights.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        for (int i = 0; i < highlights.Count; i++)
        {
            if (i > 0)
            {
                text.Append('\n');
            }

            text.Append("- ").Append(highlights[i]);
        }

        return text.ToString();
    }
}
