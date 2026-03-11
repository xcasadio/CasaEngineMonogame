#if EDITOR

using System;
using CasaEngine.EditorUI.Inputs;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Windows;

namespace CasaEngine.EditorUI.Controls;

/// <summary>
/// Lightweight WPF control that displays the output of one <see cref="RenderView"/>
/// rendered by the shared <see cref="EngineHost"/>.
///
/// <para>
/// Unlike <see cref="GameEditor"/>, this control does NOT own a
/// <see cref="Framework.Game.CasaEngineGame"/> or drive the game loop.  It extends
/// <see cref="D3D11Host"/> only to leverage the existing D3D11/D3D9/WPF interop
/// infrastructure (per-control back-buffer management and D3D11Image presentation).
/// Its <see cref="Render"/> override blits the view's
/// <see cref="RenderTargetSurface.Texture"/> into the D3D11Host back buffer each frame.
/// </para>
///
/// <para><b>Typical usage</b></para>
/// <list type="number">
///   <item>Place a <c>ViewportControl</c> anywhere in a XAML panel.</item>
///   <item>After <see cref="EngineHost.Started"/> has fired, call
///         <see cref="Attach(EngineHost, ViewId)"/> with the host and a view id
///         previously returned from <see cref="EngineHost.RegisterEditorView"/>.</item>
///   <item>Call <see cref="Detach"/> (or let the tab close naturally via Dispose) to
///         release the view when the tab is no longer needed.</item>
/// </list>
///
/// <para>Implements <see cref="IViewHost"/> so that <see cref="ViewManager"/>
/// automatically receives resize and close events.</para>
/// </summary>
public sealed class ViewportControl : D3D11Host, IViewHost
{
    private EngineHost? _engineHost;
    private ViewId      _viewId = ViewId.Empty;

    private SpriteBatch?  _blitBatch;
    private bool           _contentLoaded;

    // Per-viewport input (created in Initialize() once the GraphicsDevice is ready).
    private WpfKeyboard? _keyboard;
    private WpfMouse?    _mouse;          // conservé pour compatibilité (non utilisé pour l'état)
    // Cache de bornes-écran mis à jour sur le thread WPF, partagé entre les deux providers.
    private ViewportBoundsCache? _boundsCache;
    // Providers Win32 — indépendants du routing WPF et du hit-testing D3D11.
    private RawKeyboardProvider? _rawKeyboard;
    private RawMouseProvider?    _rawMouse;

    // ---- IViewHost ----
    // Width/Height are explicit to avoid name collision with FrameworkElement.Width/Height (double).

    /// <inheritdoc/>
    public ViewId ViewId => _viewId;

    int IViewHost.Width  => (int)ActualWidth;
    int IViewHost.Height => (int)ActualHeight;

    /// <inheritdoc/>
    public bool IsVisible => Visibility == Visibility.Visible && _contentLoaded;

    /// <inheritdoc/>
    public event Action<IViewHost, int, int>? Resized;

    /// <inheritdoc/>
    public event Action<IViewHost>? Closed;

    /// <inheritdoc/>
    public void NotifyResized(int newWidth, int newHeight)
    {
        Resized?.Invoke(this, newWidth, newHeight);

        var ctx = GetContext();
        if (ctx == null)
        {
            return;
        }

        // Resize the off-screen render target (debounced; applied on next Render pass).
        ctx.Surface?.RequestResize(newWidth, newHeight);

        // Keep the camera projection matrices in sync.
        ctx.Camera?.OnScreenResized(newWidth, newHeight);

        // Ensure an OnDemand view re-renders at least once at the new size.
        if (_engineHost?.ViewManager != null &&
            _engineHost.ViewManager.TryGetView(_viewId, out var view))
        {
            view.Invalidate();
        }
    }

    // ---- Attachment ----

    /// <summary>Whether this control is currently attached to a view on an <see cref="EngineHost"/>.</summary>
    public bool IsAttached => !_viewId.IsEmpty && _engineHost != null;

    /// <summary>
    /// Attaches this control to an existing view registered on <paramref name="host"/>.
    /// <paramref name="viewId"/> must have been created via
    /// <see cref="EngineHost.RegisterEditorView"/> before this call.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when already attached.  Call <see cref="Detach"/> first.
    /// </exception>
    public void Attach(EngineHost host, ViewId viewId)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (IsAttached)
        {
            throw new InvalidOperationException(
                "ViewportControl is already attached. Call Detach() before re-attaching.");
        }

        _engineHost = host;
        _viewId     = viewId;

        if (host.ViewManager != null &&
            host.ViewManager.TryGetView(viewId, out var view))
        {
            // Register ourselves as the IViewHost so the pipeline can query our
            // size/visibility, then subscribe ViewManager to our Resized/Closed events.
            view.Host = this;
            host.ViewManager.HookViewHost(view);
        }

        // _rawKeyboard/_rawMouse/_boundsCache sont null ici (Initialize n'a pas encore couru).
        // Initialize() les enregistrera une fois prêts.
        if (_rawKeyboard != null && _rawMouse != null && _boundsCache != null)
        {
            host.SetActiveViewportInput(viewId, _rawKeyboard, _rawMouse, _boundsCache);
        }
    }

    /// <summary>Detaches from the current view without removing or disposing it.</summary>
    public void Detach()
    {
        if (!IsAttached)
        {
            return;
        }

        _engineHost?.ClearViewportInput(_viewId);

        if (_engineHost?.ViewManager != null &&
            _engineHost.ViewManager.TryGetView(_viewId, out var view) &&
            view.Host == this)
        {
            // Unhook while Host is still non-null so ViewManager knows which host to unsubscribe.
            _engineHost.ViewManager.UnhookViewHost(view);
            view.Host = null;
        }

        _engineHost = null;
        _viewId     = ViewId.Empty;
    }

    // ---- D3D11Host abstract / virtual overrides ----

    /// <inheritdoc/>
    protected override void Initialize()
    {
        // Do NOT call base.Initialize(): D3D11Host.Initialize() looks up an
        // IGraphicsDeviceManager in Services.  ViewportControl does not register one;
        // it relies on the shared GraphicsDevice already created by EngineHost.
        // Calling base would throw NotSupportedException.
        _blitBatch = new SpriteBatch(GraphicsDevice);
        Focusable  = true;

        // Per-viewport input providers.  WpfKeyboard/WpfMouse now accept D3D11Host
        // so we can create them here (widened in PR6).
        _keyboard    = new WpfKeyboard(this);
        _mouse       = new WpfMouse(this);
        _boundsCache = new ViewportBoundsCache();
        _rawKeyboard = new RawKeyboardProvider(_boundsCache);
        _rawMouse    = new RawMouseProvider(_boundsCache);

        // Met a jour le cache de bornes-ecran sur le thread WPF chaque fois que
        // le layout change (redimensionnement, deplacement de fenetre, etc.).
        LayoutUpdated += (_, _) => _boundsCache.Update(this);
        SizeChanged   += (_, _) => _boundsCache.Update(this);

        // Activate the corresponding view in ViewManager on mouse-enter so that camera
        // navigation shortcuts and gizmo operations target the hovered viewport.
        // MouseLeave n'est plus nécessaire — IsCursorOverViewport() fait le check Win32.
        MouseEnter += (_, _) =>
        {
            // Activate the view (makes this viewport the target of camera/gizmo operations)
            // WITHOUT stealing keyboard focus.  During a drag-drop, Focus() would interfere
            // with the WPF/OLE drag state machine.  During normal navigation, Focus() here
            // is unnecessary because the FocusOnMouseOver path (PreviewMouseDown below) is
            // cleaner semantically: focus should follow clicks/interactions, not mere hover.
            ActivateThisView(requestFocus: false);
        };

        // Acquire keyboard focus on click (not on hover) so that:
        //   (a) drag-drop operations are never disrupted by an unwanted Focus() call, and
        //   (b) keyboard shortcuts target the viewport the user interacted with.
        PreviewMouseDown += (_, _) =>
        {
            if (!IsFocused)
            {
                Focus();
            }
        };

        // Empêche WPF de router les touches de navigation (flèches, PageUp/Down, etc.)
        // vers d'autres contrôles focusables quand la souris est sur ce viewport.
        // Utilise Win32 GetCursorPos — fiable même si MouseEnter est court-circuité
        // par le hit-testing D3D11Image.
        PreviewKeyDown += (_, e) =>
        {
            if (_rawKeyboard?.IsCursorOverViewport() == true)
            {
                e.Handled = true;
            }
        };

        // Attach() est appelé AVANT Initialize() — au moment de Attach() les providers étaient null.
        // Maintenant qu'ils sont prêts, enregistrer auprès du dispatcher de l'EngineHost.
        if (_engineHost != null)
        {
            _engineHost.SetActiveViewportInput(_viewId, _rawKeyboard, _rawMouse, _boundsCache!);
        }
    }

    /// <inheritdoc/>
    protected override void LoadContent()
    {
        _contentLoaded = true;
    }

    /// <summary>
    /// Blits the view's rendered texture into D3D11Host's per-instance back buffer so
    /// the D3D9/WPF interop channel can present it.
    ///
    /// D3D11Host.OnRendering already sets <c>_cachedRenderTarget</c> as the active render
    /// target before calling this method, so we draw directly into it.
    /// </summary>
    protected override void Render(GameTime time)
    {
        if (!_contentLoaded || _viewId.IsEmpty)
        {
            return;
        }

        var src = GetViewTexture();
        if (src == null)
        {
            return;
        }

        _blitBatch!.Begin(SpriteSortMode.Immediate, BlendState.Opaque,
            SamplerState.LinearClamp, null, null);
        _blitBatch.Draw(
            src,
            new Rectangle(0, 0, Math.Max(1, (int)ActualWidth), Math.Max(1, (int)ActualHeight)),
            Color.White);
        _blitBatch.End();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 1. Fire IViewHost.Closed → ViewManager.OnHostClosed → ViewManager.Remove(view).
            Closed?.Invoke(this);
            Closed = null;

            // 2. Ask EngineHost to dispose EditorViewContext, surface and overlay components.
            //    Safe even when the view was already removed from ViewManager (UnregisterEditorView
            //    skips the Remove call if TryGetView returns false).
            if (_engineHost != null && !_viewId.IsEmpty)
            {
                _engineHost.UnregisterEditorView(_viewId);
            }

            _blitBatch?.Dispose();
            _blitBatch  = null;
            _engineHost = null;
            _viewId     = ViewId.Empty;
        }
    }

    /// <inheritdoc/>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        NotifyResized((int)sizeInfo.NewSize.Width, (int)sizeInfo.NewSize.Height);
    }

    // ---- Helpers ----

    /// <summary>
    /// Tells the <see cref="ViewManager"/> to make our view the active one.
    /// Called on <see cref="System.Windows.UIElement.MouseEnter"/> so that camera
    /// navigation and gizmo operations always target the hovered viewport.
    /// </summary>
    private void ActivateThisView(bool requestFocus = true)
    {
        if (_engineHost?.ViewManager == null || _viewId.IsEmpty)
        {
            return;
        }

        if (_engineHost.ViewManager.TryGetView(_viewId, out var view))
        {
            _engineHost.ViewManager.SetActive(view);
        }

        if (requestFocus)
        {
            Focus();
        }
    }

    /// <summary>
    /// Returns the current per-viewport keyboard state.
    /// Returns a default (all keys up) state when not yet initialized.
    /// </summary>
    public KeyboardState GetKeyboardState()
        => _keyboard?.GetState() ?? new KeyboardState();

    /// <summary>
    /// Returns the current per-viewport mouse state (position is viewport-local).
    /// Returns a default (all buttons up) state when not yet initialized.
    /// </summary>
    public MouseState GetMouseState()
        => _mouse?.GetState() ?? new MouseState();

    private EditorViewContext? GetContext()
    {
        if (_engineHost?.ViewManager == null || _viewId.IsEmpty)
        {
            return null;
        }

        return _engineHost.ViewManager.TryGetView(_viewId, out var v)
            ? v.Tag as EditorViewContext
            : null;
    }

    private Texture2D? GetViewTexture() => GetContext()?.Surface?.Texture;
}

#endif
