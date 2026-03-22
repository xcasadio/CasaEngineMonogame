using System;
using System.Collections.Generic;
using System.Linq;

namespace CasaEngine.EditorServices.ScreenEditor.Inspector;

/// <summary>
/// Central registry that maps MGUI control types to their editable properties.
/// Provides built-in descriptors for common properties and supports registration
/// of additional control-specific descriptors.
/// </summary>
public sealed class UIPropertyRegistry
{
    // ─────────────────────────────────────────────────────────────────────
    //  Built-in descriptors
    // ─────────────────────────────────────────────────────────────────────

    private static readonly UIPropertyDescriptor[] CommonLayoutDescriptors =
    {
        new("Width",               "Width",               "Layout",     typeof(int?)),
        new("Height",              "Height",              "Layout",     typeof(int?)),
        new("Margin",              "Margin",              "Layout",     typeof(string)),
        new("Padding",             "Padding",             "Layout",     typeof(string)),
        new("HorizontalAlignment", "H. Alignment",        "Layout",     typeof(string), "Stretch"),
        new("VerticalAlignment",   "V. Alignment",        "Layout",     typeof(string), "Stretch"),
        new("MinWidth",            "Min Width",           "Layout",     typeof(int?)),
        new("MinHeight",           "Min Height",          "Layout",     typeof(int?)),
        new("MaxWidth",            "Max Width",           "Layout",     typeof(int?)),
        new("MaxHeight",           "Max Height",          "Layout",     typeof(int?)),
    };

    private static readonly UIPropertyDescriptor[] CommonAppearanceDescriptors =
    {
        new("Opacity", "Opacity", "Appearance", typeof(float?), "1"),
        new("IsVisible", "Visible", "Appearance", typeof(bool?), "True"),
        new("IsEnabled", "Enabled", "Appearance", typeof(bool?), "True"),
    };

    private static readonly Dictionary<string, UIPropertyDescriptor[]> ControlSpecificDescriptors
        = new(StringComparer.Ordinal)
    {
        ["Button"] = new[]
        {
            new UIPropertyDescriptor("Content", "Content", "Button", typeof(string)),
        },
        ["TextBlock"] = new[]
        {
            new UIPropertyDescriptor("Text",      "Text",      "TextBlock", typeof(string)),
            new UIPropertyDescriptor("FontSize",  "Font Size", "TextBlock", typeof(int?)),
            new UIPropertyDescriptor("WrapText",  "Wrap Text", "TextBlock", typeof(bool?), "False"),
            new UIPropertyDescriptor("IsBold",    "Bold",      "TextBlock", typeof(bool?), "False"),
            new UIPropertyDescriptor("IsItalic",  "Italic",    "TextBlock", typeof(bool?), "False"),
        },
        ["TextBox"] = new[]
        {
            new UIPropertyDescriptor("Text",        "Text",        "TextBox", typeof(string)),
            new UIPropertyDescriptor("Placeholder", "Placeholder", "TextBox", typeof(string)),
        },
        ["Window"] = new[]
        {
            new UIPropertyDescriptor("TitleText",        "Title",           "Window", typeof(string)),
            new UIPropertyDescriptor("Width",            "Width",           "Layout", typeof(int?)),
            new UIPropertyDescriptor("Height",           "Height",          "Layout", typeof(int?)),
            new UIPropertyDescriptor("CanCloseWindow",   "Can Close",       "Window", typeof(bool?), "True"),
            new UIPropertyDescriptor("IsUserResizable",  "User Resizable",  "Window", typeof(bool?), "True"),
            new UIPropertyDescriptor("WindowStyle",      "Window Style",    "Window", typeof(string), "Default"),
        },
        ["StackPanel"] = new[]
        {
            new UIPropertyDescriptor("Orientation", "Orientation", "StackPanel", typeof(string), "Vertical"),
            new UIPropertyDescriptor("Spacing",     "Spacing",     "StackPanel", typeof(int?),   "0"),
        },
        ["DockPanel"] = new[]
        {
            new UIPropertyDescriptor("LastChildFill", "Last Child Fill", "DockPanel", typeof(bool?), "True"),
        },
        ["Border"] = new[]
        {
            new UIPropertyDescriptor("BorderThickness", "Border Thickness", "Border", typeof(string)),
        },
        ["ProgressBar"] = new[]
        {
            new UIPropertyDescriptor("Minimum",  "Minimum",  "ProgressBar", typeof(float?), "0"),
            new UIPropertyDescriptor("Maximum",  "Maximum",  "ProgressBar", typeof(float?), "100"),
            new UIPropertyDescriptor("Value",    "Value",    "ProgressBar", typeof(float?), "50"),
        },
        ["Slider"] = new[]
        {
            new UIPropertyDescriptor("Minimum",  "Minimum", "Slider", typeof(float?), "0"),
            new UIPropertyDescriptor("Maximum",  "Maximum", "Slider", typeof(float?), "100"),
            new UIPropertyDescriptor("Value",    "Value",   "Slider", typeof(float?), "0"),
        },
        ["CheckBox"] = new[]
        {
            new UIPropertyDescriptor("Content",    "Content",  "CheckBox", typeof(string)),
            new UIPropertyDescriptor("IsChecked",  "Checked",  "CheckBox", typeof(bool?), "False"),
        },
        ["RadioButton"] = new[]
        {
            new UIPropertyDescriptor("Content",    "Content",  "RadioButton", typeof(string)),
            new UIPropertyDescriptor("IsChecked",  "Checked",  "RadioButton", typeof(bool?), "False"),
            new UIPropertyDescriptor("GroupName",  "Group",    "RadioButton", typeof(string)),
        },
        ["ComboBox"] = new[]
        {
            new UIPropertyDescriptor("Placeholder", "Placeholder", "ComboBox", typeof(string)),
        },
        ["ListBox"] = new[]
        {
            new UIPropertyDescriptor("SelectionMode", "Selection Mode", "ListBox", typeof(string), "Single"),
        },
        ["Image"] = new[]
        {
            new UIPropertyDescriptor("Source", "Source", "Image", typeof(string)),
        },
    };

    // ─────────────────────────────────────────────────────────────────────
    //  Custom registrations
    // ─────────────────────────────────────────────────────────────────────

    private readonly Dictionary<string, List<UIPropertyDescriptor>> _customDescriptors
        = new(StringComparer.Ordinal);

    /// <summary>Gets or creates the singleton instance.</summary>
    public static UIPropertyRegistry Default { get; } = new();

    /// <summary>
    /// Returns all descriptors for a given control type.
    /// Includes common layout/appearance descriptors plus control-specific ones
    /// and any custom-registered ones.
    /// </summary>
    public IReadOnlyList<UIPropertyDescriptor> GetDescriptors(string controlType)
    {
        if (string.IsNullOrWhiteSpace(controlType))
        {
            return Array.Empty<UIPropertyDescriptor>();
        }

        var result = new List<UIPropertyDescriptor>();

        // Common descriptors shared by all controls (skip for Window which manages its own size)
        if (!string.Equals(controlType, "Window", StringComparison.Ordinal))
        {
            result.AddRange(CommonLayoutDescriptors);
            result.AddRange(CommonAppearanceDescriptors);
        }

        // Control-specific descriptors (may override width/height etc. for Window)
        if (ControlSpecificDescriptors.TryGetValue(controlType, out var specific))
        {
            foreach (var d in specific)
            {
                // Replace any existing descriptor with the same name
                var existing = result.FindIndex(x => string.Equals(x.Name, d.Name, StringComparison.Ordinal));
                if (existing >= 0)
                {
                    result[existing] = d;
                }
                else
                {
                    result.Add(d);
                }
            }
        }

        // Custom-registered descriptors
        if (_customDescriptors.TryGetValue(controlType, out var custom))
        {
            result.AddRange(custom);
        }

        return result;
    }

    /// <summary>Registers an additional descriptor for a control type at runtime.</summary>
    public void RegisterDescriptor(string controlType, UIPropertyDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(controlType))
        {
            throw new ArgumentException("Control type must not be empty.", nameof(controlType));
        }

        if (!_customDescriptors.TryGetValue(controlType, out var list))
        {
            list = new List<UIPropertyDescriptor>();
            _customDescriptors[controlType] = list;
        }

        list.Add(descriptor);
    }
}
