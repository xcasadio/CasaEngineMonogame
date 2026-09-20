using CasaEngine.Engine.Environment;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;

namespace CasaEngine.Framework.UI.MGUI;

/// <summary>
/// Turns a UI screen asset into a live <see cref="MGWindow"/>: resolves the XAML file the asset points at,
/// parses it, and hands the window back so a screen can look its controls up by name and push data into them.
/// <para/>
/// The window comes back <b>unregistered</b>. Adding it to <see cref="MGDesktop.Windows"/> belongs to
/// <see cref="ScreenStack"/>, which does it on push and undoes it on pop; a loader that registered windows
/// itself would leave one behind every time a screen was rebuilt.
/// <para/>
/// Parsing runs in <see cref="XamlLoaderMode.Strict"/>. That is the point of loading a screen this way: an
/// unknown element or a name declared twice fails here, at load, instead of silently breaking a later lookup
/// by name. Every failure of the document surfaces as one type, <see cref="XamlLoaderException"/>, carrying
/// the file and -- when the failure has one -- the line and column. That includes the duplicate name a
/// control template applies, which MGUI can only reject while the built tree is being attached, long after
/// <see cref="XAMLParser"/> has returned.
/// </summary>
public static class UIScreenLoader
{
    private const string DocumentKind = "UIScreen";

    /// <summary>Loads the screen a <see cref="UIScreenAsset"/> describes.</summary>
    /// <param name="desktop">The desktop the window is built against. Not modified.</param>
    /// <param name="asset">The deserialized screen envelope.</param>
    /// <param name="assetFilePath">
    /// Path of the envelope file <paramref name="asset"/> was read from, used as the first base directory for
    /// <see cref="UIScreenAsset.SourceXamlFile"/>. It is a parameter rather than a member of the asset because
    /// nothing populates a path on this asset type on the way in: every caller builds it by hand and sets
    /// <c>FileName</c> itself, or leaves it empty.
    /// </param>
    /// <exception cref="InvalidOperationException"><paramref name="asset"/> declares no source XAML file.</exception>
    /// <exception cref="FileNotFoundException">No candidate path for the source XAML file exists on disk.</exception>
    /// <exception cref="XamlLoaderException">The document failed to parse, validate, or attach.</exception>
    public static MGWindow Load(MGDesktop desktop, UIScreenAsset asset, string assetFilePath)
    {
        if (desktop == null)
        {
            throw new ArgumentNullException(nameof(desktop));
        }

        if (asset == null)
        {
            throw new ArgumentNullException(nameof(asset));
        }

        if (string.IsNullOrWhiteSpace(assetFilePath))
        {
            throw new ArgumentException("A UIScreen asset file path is required to resolve its XAML file.", nameof(assetFilePath));
        }

        var sourceXamlPath = ResolveSourceXamlPath(asset, assetFilePath);
        return Load(desktop, XamlDocumentSource.FromFile(sourceXamlPath), asset.ThemeName);
    }

    /// <summary>
    /// Loads a screen straight from a XAML document, for a screen that has no catalogued asset -- a demo, or
    /// a test.
    /// </summary>
    /// <param name="themeName">
    /// Theme to apply to the loaded window, or null to keep whatever the document itself declared. An unknown
    /// name is not an error: <see cref="MGResources.GetThemeOrDefault"/> falls back to the default theme.
    /// </param>
    /// <exception cref="XamlLoaderException">The document failed to parse, validate, or attach.</exception>
    public static MGWindow Load(MGDesktop desktop, XamlDocumentSource source, string themeName = null)
    {
        if (desktop == null)
        {
            throw new ArgumentNullException(nameof(desktop));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        MGWindow window;

        try
        {
            window = XAMLParser.LoadRootWindow(desktop, source, XamlLoaderMode.Strict);
        }
        catch (XamlLoaderException)
        {
            // Already describes this document, with its own position. Let it through unchanged.
            throw;
        }
        catch (Exception exception)
        {
            // Building the tree and attaching it are two steps and MGUI only wraps the first, so a failure of
            // the second arrives here bare. Re-describe it the way the loader would have, and keep the
            // original as the inner exception.
            throw new XamlLoaderException(XamlLoaderDiagnostic.FromException(exception, source, DocumentKind), exception);
        }

        if (!string.IsNullOrWhiteSpace(themeName))
        {
            window.Theme = desktop.Resources.GetThemeOrDefault(themeName);
        }

        return window;
    }

    /// <summary>
    /// Finds the XAML file an asset points at: an absolute path as given, otherwise relative to the envelope,
    /// otherwise relative to the project. Every candidate must exist on disk to be returned, so this either
    /// hands back a readable file or says which paths it tried.
    /// </summary>
    private static string ResolveSourceXamlPath(UIScreenAsset asset, string assetFilePath)
    {
        if (string.IsNullOrWhiteSpace(asset.SourceXamlFile))
        {
            throw new InvalidOperationException($"UIScreen asset '{assetFilePath}' is missing 'SourceXamlFile'.");
        }

        if (Path.IsPathRooted(asset.SourceXamlFile))
        {
            if (!File.Exists(asset.SourceXamlFile))
            {
                throw new FileNotFoundException(
                    $"UIScreen XAML file of asset '{assetFilePath}' was not found at its absolute path.",
                    asset.SourceXamlFile);
            }

            return asset.SourceXamlFile;
        }

        string relativeToAsset = null;
        var assetDirectory = Path.GetDirectoryName(assetFilePath);

        if (!string.IsNullOrWhiteSpace(assetDirectory))
        {
            relativeToAsset = Path.GetFullPath(Path.Combine(assetDirectory, asset.SourceXamlFile));

            if (File.Exists(relativeToAsset))
            {
                return relativeToAsset;
            }
        }

        var projectPath = EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath);
        var relativeToProject = Path.GetFullPath(Path.Combine(projectPath, asset.SourceXamlFile));

        if (File.Exists(relativeToProject))
        {
            return relativeToProject;
        }

        throw new FileNotFoundException(
            $"UIScreen XAML file '{asset.SourceXamlFile}' of asset '{assetFilePath}' was not found. Tried " +
            $"'{relativeToAsset ?? "(the asset has no directory)"}' and '{relativeToProject}'.",
            asset.SourceXamlFile);
    }
}
