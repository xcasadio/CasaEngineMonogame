namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

/// <summary>
/// A named resource entry stored in <see cref="UIScreenDocument.Resources"/>.
/// The value is a raw XAML fragment that will be emitted inside <c>Window.Resources</c>.
/// </summary>
public sealed class UIScreenResourceEntry
{
    /// <summary>Resource key (x:Key value).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Raw XAML string for the resource value.</summary>
    public string XamlValue { get; set; } = string.Empty;
}
