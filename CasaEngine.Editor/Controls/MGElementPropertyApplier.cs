using System;
using System.Globalization;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Applies a single XAML property value directly to a live <see cref="MGElement"/> instance,
/// bypassing a full preview rebuild for common, hot-path properties.
/// <para/>
/// Returns <c>true</c> when the patch was applied successfully.
/// Returns <c>false</c> when the property is not supported — callers should fall back to a full rebuild.
/// </summary>
internal static class MGElementPropertyApplier
{
    /// <summary>
    /// Tries to apply <paramref name="propertyName"/> = <paramref name="value"/> to
    /// <paramref name="element"/>.  Returns true on success.
    /// </summary>
    public static bool TryApply(MGElement element, string propertyName, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        switch (propertyName)
        {
            // ── Appearance ────────────────────────────────────────────────
            case "Opacity":
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
                {
                    element.Opacity = Math.Clamp(opacity, 0f, 1f);
                    return true;
                }
                return false;

            case "IsVisible":
                if (bool.TryParse(value, out var visible))
                {
                    element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    return true;
                }
                return false;

            case "IsEnabled":
                if (bool.TryParse(value, out var enabled))
                {
                    element.IsEnabled = enabled;
                    return true;
                }
                return false;

            // ── Size ──────────────────────────────────────────────────────
            case "Width":
                element.PreferredWidth = TryParseNullableInt(value);
                return true;

            case "Height":
                element.PreferredHeight = TryParseNullableInt(value);
                return true;

            // ── TextBlock-specific ────────────────────────────────────────
            case "Text" when element is MGTextBlock tb:
                tb.Text = value ?? string.Empty;
                return true;

            case "WrapText" when element is MGTextBlock tbw:
                if (bool.TryParse(value, out var wrap))
                {
                    tbw.WrapText = wrap;
                    return true;
                }
                return false;

            case "FontSize" when element is MGTextBlock tbf:
                if (int.TryParse(value, out var fontSize) && fontSize > 0)
                {
                    tbf.FontSize = fontSize;
                    return true;
                }
                return false;

            // ── TextBox-specific ──────────────────────────────────────────
            case "Text" when element is MGTextBox textBox:
                textBox.Text = value ?? string.Empty;
                return true;

            // ── CheckBox-specific ─────────────────────────────────────────
            case "IsChecked" when element is MGCheckBox cb:
                if (bool.TryParse(value, out var isChecked))
                {
                    cb.IsChecked = isChecked;
                    return true;
                }
                return false;

            // ── StackPanel-specific ───────────────────────────────────────
            case "Orientation" when element is MGStackPanel sp:
                if (Enum.TryParse<Orientation>(value, ignoreCase: true, out var orientation))
                {
                    sp.Orientation = orientation;
                    return true;
                }
                return false;
        }

        return false;
    }

    private static int? TryParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), out var result) ? result : null;
    }
}
