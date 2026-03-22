using System.Collections.Generic;

namespace CasaEngine.EditorServices.ScreenEditor.Toolbox;

/// <summary>
/// Describes a single control type that can be created from the toolbox.
/// </summary>
public sealed class UIControlRegistryEntry
{
    /// <summary>The XAML element name used when serializing (e.g. "Button", "StackPanel").</summary>
    public string ControlType { get; }

    /// <summary>Human-readable name shown in the toolbox.</summary>
    public string DisplayName { get; }

    /// <summary>Toolbox group (e.g. "Layout", "Controls", "Input").</summary>
    public string Category { get; }

    /// <summary>
    /// Default property values written into the new node.
    /// Keys are property names; values are serialized strings (nullable = omit attribute).
    /// </summary>
    public IReadOnlyDictionary<string, string?> DefaultProperties { get; }

    public UIControlRegistryEntry(
        string controlType,
        string displayName,
        string category,
        IReadOnlyDictionary<string, string?>? defaultProperties = null)
    {
        ControlType      = controlType;
        DisplayName      = displayName;
        Category         = category;
        DefaultProperties = defaultProperties ?? new Dictionary<string, string?>();
    }
}
