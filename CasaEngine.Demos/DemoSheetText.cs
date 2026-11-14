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
    public static string FormatControls(IReadOnlyList<DemoControl> controls)
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

            text.Append(controls[i].Keyboard);
            if (!string.IsNullOrEmpty(controls[i].GamePad))
            {
                text.Append(" / ").Append(controls[i].GamePad);
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
