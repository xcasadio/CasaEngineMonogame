using CasaEngine.Engine.Input.InputDeviceStateProviders;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.DebugTools;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Game.Components.Editor;

/// <summary>
/// Carries all data that is specific to one editor viewport (<see cref="RenderView"/>).
///
/// An <see cref="EditorViewContext"/> is created by <c>EngineHost.RegisterEditorView()</c>
/// and stored in <see cref="RenderView.Tag"/> so the rendering pipeline and WPF
/// controls can retrieve it without any global state.
///
/// Lifecycle:
/// <list type="bullet">
///   <item>Created when a viewport tab is opened / registered with the EngineHost.</item>
///   <item>Disposed when the tab is closed / unregistered — disposes the
///         <see cref="Surface"/> and (after PR 5) the standalone overlay components.</item>
/// </list>
/// </summary>
public sealed class EditorViewContext : IDisposable
{
    private bool _disposed;

    // ---- Identity ----

    /// <summary>Stable key of the associated <see cref="RenderView"/>.</summary>
    public ViewId ViewId { get; }

    /// <summary>The engine view this context is attached to.</summary>
    public RenderView RenderView { get; }

    // ---- Rendering data ----

    /// <summary>World rendered in this viewport (may differ per tab, e.g. entity-preview worlds).</summary>
    public World.World? World { get; set; }

    /// <summary>Camera used to compute View / Projection matrices for this viewport.</summary>
    public CameraComponent? Camera { get; set; }

    /// <summary>
    /// The entity that owns the <see cref="Camera"/> component.
    /// Updated every frame by <c>EngineHost.Update()</c> so ArcBall / 2-D camera
    /// navigation scripts receive their per-frame tick.
    /// </summary>
    public Entity? CameraEntity { get; set; }

    /// <summary>
    /// Off-screen render target for this viewport.
    /// Owned by this context — disposed in <see cref="Dispose"/>.
    /// </summary>
    public RenderTargetSurface? Surface { get; set; }

    // ---- Editor overlay components ----
    // NOTE: In PR 2 these are still DrawableGameComponent instances managed by
    //       Game.Components.  PR 5 will extract them into standalone objects driven
    //       per-view via OverlayViewPipeline, at which point Dispose() below will
    //       also clean them up.

    /// <summary>Gizmo (translate / rotate / scale) overlay for this viewport. Null for 2-D views.</summary>
    public TransformGizmoComponent? Gizmo { get; set; }

    /// <summary>Ground-plane grid overlay for this viewport. Null for 2-D views.</summary>
    public DebugGridComponent? Grid { get; set; }

    /// <summary>XYZ axis indicator drawn in a corner of this viewport. Null for 2-D views.</summary>
    public DebugAxisComponent? Axis { get; set; }

    // ---- Per-viewport input providers ----
    // Each ViewportControl supplies its own WpfKeyboard / WpfMouse scoped to that
    // WPF element.  The EngineHost plumbs these into the InputRouter so input is
    // routed to the correct RenderView.

    /// <summary>Keyboard state provider scoped to this viewport's WPF control.</summary>
    public IKeyboardStateProvider? KeyboardProvider { get; set; }

    /// <summary>Mouse state provider scoped to this viewport's WPF control.</summary>
    public IMouseStateProvider? MouseProvider { get; set; }

    // ---- Metadata ----

    /// <summary>Human-readable name shown in debug overlays and logs.</summary>
    public string Name { get; set; }

    /// <summary>Logical role of this viewport (world, entity preview, sprite, …).</summary>
    public EditorViewType ViewType { get; set; }

    // ---- Constructor ----

    public EditorViewContext(
        ViewId       viewId,
        RenderView   renderView,
        string       name,
        EditorViewType viewType)
    {
        ViewId     = viewId;
        RenderView = renderView;
        Name       = name;
        ViewType   = viewType;

        // Back-link the context into the view's Tag so the rendering pipeline
        // and editor code can retrieve it without extra dictionaries.
        renderView.Tag = this;
    }

    // ---- IDisposable ----

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Surface is owned by this context — dispose its render target.
        Surface?.Dispose();
        Surface = null;

        // Clear the back-link to avoid dangling references.
        if (RenderView.Tag == this)
        {
            RenderView.Tag = null;
        }

        // Gizmo / Grid / Axis disposal is handled in PR 5 when they become
        // standalone objects.  As DrawableGameComponents they are removed from
        // Game.Components by CasaEngineGame / EngineHost when the view is torn down.
    }
}
