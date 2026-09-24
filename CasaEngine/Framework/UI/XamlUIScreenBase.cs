using CasaEngine.Framework.Assets;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;

namespace CasaEngine.Framework.UI;

/// <summary>
/// Base class for a screen whose control tree is declared in XAML instead of built in C#.
/// <para/>
/// The division of labour: the XAML owns what is static -- the tree, the names, the layout, the styles --
/// and the code owns what changes: values, visibility, positions that depend on the resolution, and event
/// subscriptions. A subclass implements <see cref="OnWindowLoaded"/>, looks its controls up by name through
/// <see cref="FindControl{T}"/>, and keeps what it found in fields. It must never look a control up by name
/// from <see cref="UIScreenBase.Update"/>: that runs every frame.
/// <para/>
/// <b>Why <see cref="BuildWindow"/> takes an <see cref="MGDesktop"/>:</b> so it can be tested. A
/// <see cref="UIRoot"/> is sealed around a MonoGame backend and cannot be built without a graphics device,
/// so <see cref="OnInitialize"/> does nothing but hand this method the desktop it already holds. Tests drive
/// <see cref="BuildWindow"/> directly on a headless desktop, the way the dialogue screen's tests already do.
/// </summary>
public abstract class XamlUIScreenBase : UIScreenBase, IDisposable
{
    private readonly UIScreenAsset _asset;
    private readonly string _assetFilePath;
    private readonly XamlDocumentSource _source;
    private readonly string _themeName;

    // Set only by the constructor that acquires its envelope through the asset manager (ADR-0037,
    // ADR-0038); null for a screen built from a caller-supplied asset or an embedded document, which own
    // nothing here to give back.
    private readonly AssetHandle<UIScreenAsset> _assetHandle;

    /// <summary>The window built from the XAML, or null until the screen has been initialized.</summary>
    protected MGWindow Window { get; private set; }

    /// <summary>Builds the screen from a catalogued asset.</summary>
    /// <param name="assetFilePath">Path of the envelope file <paramref name="asset"/> was read from.</param>
    protected XamlUIScreenBase(UIScreenAsset asset, string assetFilePath)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));

        if (string.IsNullOrWhiteSpace(assetFilePath))
        {
            throw new ArgumentException("A UIScreen asset file path is required to resolve its XAML file.", nameof(assetFilePath));
        }

        _assetFilePath = assetFilePath;
    }

    /// <summary>Builds the screen from a XAML document, for a screen that has no catalogued asset.</summary>
    protected XamlUIScreenBase(XamlDocumentSource source, string themeName = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _themeName = themeName;
    }

    /// <summary>
    /// Builds the screen from a catalogued asset, acquired through <paramref name="assetContentManager"/>
    /// and held for the screen's lifetime (ADR-0037, ADR-0038). The screen must be disposed to give the
    /// hold back; <see cref="Dispose"/> does that.
    /// </summary>
    /// <param name="assetIdOrName">
    /// The screen asset's id, resolved by <paramref name="assetContentManager"/>, or -- when it does not parse as
    /// a <see cref="Guid"/> -- its name in <see cref="AssetCatalog"/>. Mirrors the resolution
    /// <see cref="CasaEngine.Framework.UI.Backend.MonoGame.Assets.CasaUIAssetProvider"/> already applies to
    /// image sources (ADR-0038).
    /// </param>
    /// <exception cref="InvalidOperationException">No screen asset of that id or name can be resolved.</exception>
    protected XamlUIScreenBase(AssetContentManager assetContentManager, string assetIdOrName)
    {
        ArgumentNullException.ThrowIfNull(assetContentManager);

        if (string.IsNullOrWhiteSpace(assetIdOrName))
        {
            throw new ArgumentException("A UIScreen asset id or name is required.", nameof(assetIdOrName));
        }

        // An id is resolved by the asset manager itself (its runtime context: the project's catalogue in a game),
        // so a manager given its own catalogue -- a test's -- needs nothing from the global one. Only a name is
        // looked up in the catalogue, the manager having no lookup by name.
        if (!Guid.TryParse(assetIdOrName, out var assetId))
        {
            var assetInfo = AssetCatalog.Get(assetIdOrName)
                ?? throw new InvalidOperationException($"No screen asset '{assetIdOrName}' is registered in this project.");
            assetId = assetInfo.Id;
        }

        _assetHandle = assetContentManager.Acquire<UIScreenAsset>(assetId);
        _asset = _assetHandle.Asset;
        _assetFilePath = assetContentManager.ResolveAssetFullPath(_asset.FileName);
    }

    /// <summary>
    /// Loads the XAML and hands the window to <see cref="OnWindowLoaded"/>. This is the whole of the screen's
    /// construction, and the seam a test drives instead of <see cref="OnInitialize"/>.
    /// </summary>
    /// <exception cref="XamlLoaderException">The document failed to parse, validate, or attach.</exception>
    public MGWindow BuildWindow(MGDesktop desktop)
    {
        if (desktop == null)
        {
            throw new ArgumentNullException(nameof(desktop));
        }

        Window = _asset != null
            ? UIScreenLoader.Load(desktop, _asset, _assetFilePath)
            : UIScreenLoader.Load(desktop, _source, _themeName);

        OnWindowLoaded(Window);
        return Window;
    }

    /// <summary>
    /// Called once, right after the XAML has been loaded. Look the controls up by name here, keep them in
    /// fields, and subscribe to what the screen needs.
    /// </summary>
    protected abstract void OnWindowLoaded(MGWindow window);

    /// <summary>
    /// Finds a control the XAML is expected to declare, and says precisely what is wrong when it does not.
    /// <para/>
    /// This exists because <see cref="MGWindow.TryGetElementByName{T}"/> answers false for two different
    /// mistakes -- a name that is absent, and a name that belongs to another kind of control -- and answers
    /// with a null a screen would only trip over later, far from the cause.
    /// </summary>
    /// <exception cref="InvalidOperationException">The screen is not loaded, the name is absent, or it names
    /// a control of another type.</exception>
    protected T FindControl<T>(string name) where T : MGElement
    {
        if (Window == null)
        {
            throw new InvalidOperationException($"Screen '{GetType().Name}' looked up control '{name}' before its XAML was loaded.");
        }

        if (!Window.TryGetElementByName(name, out MGElement element))
        {
            throw new InvalidOperationException(
                $"Screen '{GetType().Name}' expects a control named '{name}', which '{SourceDescription}' does not declare.");
        }

        if (element is not T typed)
        {
            throw new InvalidOperationException(
                $"Screen '{GetType().Name}' expects '{name}' to be a {typeof(T).Name}, but '{SourceDescription}' " +
                $"declares it as a {element.GetType().Name}.");
        }

        return typed;
    }

    /// <summary>What to call the XAML document in an error message.</summary>
    private string SourceDescription
        => _asset != null
            ? _assetFilePath
            : _source.FilePath ?? _source.DisplayName ?? "the screen's XAML";

    /// <inheritdoc/>
    protected override void OnInitialize(UIRoot root)
    {
        BuildWindow(root.Desktop);
    }

    /// <inheritdoc/>
    public override IEnumerable<MGWindow> GetWindows()
    {
        if (Window != null)
        {
            yield return Window;
        }
    }

    /// <summary>
    /// Gives back the hold this screen took on its envelope, when it was built through the asset-manager
    /// constructor. A screen built from a caller-supplied asset or an embedded document holds nothing here,
    /// so this is a no-op for it. Idempotent, like <see cref="AssetHandle{T}.Dispose"/>.
    /// </summary>
    public virtual void Dispose()
    {
        _assetHandle?.Dispose();
    }
}
