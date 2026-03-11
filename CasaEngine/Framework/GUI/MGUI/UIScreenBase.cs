using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.GUI;

/// <summary>
/// Convenience base class for <see cref="IUIScreen"/> implementations.
/// Subclasses implement <see cref="OnInitialize"/> to build their windows and
/// bind data; all other lifecycle methods are virtual no-ops by default.
/// </summary>
public abstract class UIScreenBase : IUIScreen
{
    /// <inheritdoc/>
    public abstract UILayer Layer { get; }

    /// <inheritdoc/>
    public virtual bool IsModal => false;

    /// <inheritdoc/>
    public virtual bool BlocksViewsBelow => IsModal;

    /// <summary>The <see cref="UIRoot"/> this screen was initialised with.</summary>
    protected UIRoot? Root { get; private set; }

    private bool _initialized;

    /// <summary>
    /// Override this to build <see cref="MGWindow"/> instances, bind data sources,
    /// and set up event subscriptions. Called once on first push.
    /// </summary>
    protected abstract void OnInitialize(UIRoot root);

    // ---- IUIScreen ----

    /// <inheritdoc/>
    public void Initialize(UIRoot root)
    {
        if (_initialized) return;
        _initialized = true;
        Root = root;
        OnInitialize(root);
    }

    /// <inheritdoc/>
    public virtual void Show() { }

    /// <inheritdoc/>
    public virtual void Hide() { }

    /// <inheritdoc/>
    public virtual void Update(GameTime gameTime) { }

    /// <inheritdoc/>
    public abstract IEnumerable<MGWindow> GetWindows();
}
