using System;
using System.IO;
using MGUI.Core.UI.XAML;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Finds a demo screen's XAML. The demos have no catalogued project, so their screens are plain documents
/// shipped beside the executable rather than `.uiscreen` assets.
/// <para/>
/// Anchored on <see cref="AppContext.BaseDirectory"/>, not the current directory: the files are copied to the
/// output folder by the project, so this resolves whether the demo is launched from the project directory or
/// from `bin`.
/// </summary>
internal static class DemoScreenXaml
{
    public static XamlDocumentSource Source(string fileName)
        => XamlDocumentSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Content", "Screens", fileName));
}
