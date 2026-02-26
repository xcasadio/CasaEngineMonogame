#if EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CasaEngine.Core.Log;
using CasaEngine.EditorUI.Inputs;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls;

/// <summary>
/// Central runtime host for the editor — owns the single <see cref="CasaEngineGame"/>
/// instance and drives the shared game loop.
///
/// <para>
/// <b>Architecture (multi-view migration):</b><br/>
/// A hidden 1×1 WPF control placed in <c>MainWindow</c>.
/// All editor viewport tabs register themselves here via
/// <see cref="RegisterEditorView(EditorViewDefinition)"/> to get a
/// <see cref="ViewId"/>. Each tab then displays the
/// <see cref="RenderTargetSurface.Texture"/> of its view via a
/// lightweight <c>ViewportControl</c>.
/// </para>
///
/// <para>
/// <b>Current state (PR 3):</b><br/>
/// Infrastructure only — existing <see cref="GameEditor"/> tabs still work
/// alongside <c>EngineHost</c>.  PR 7 migrates the tabs to use this host.
/// </para>
/// </summary>
public sealed class EngineHost : WpfGame
{
    // ---- Singleton access ----
    // (The editor has exactly one project open and one EngineHost at a time.)
    private static EngineHost? _instance;

    /// <summary>
    /// The single active <see cref="EngineHost"/> instance, or <see langword="null"/>
    /// before the host control has been loaded by WPF.
    /// </summary>
    public static EngineHost? Instance => _instance;

    // ---- State ----
    private CasaEngineGame? _game;
    private bool            _initialized;

    private readonly Dictionary<ViewId, EditorViewContext> _viewContexts = new();

    // ---- Per-viewport input registry (filled by ViewportControl.Initialize) ----
    private readonly Dictionary<ViewId, (RawKeyboardProvider Kbd, RawMouseProvider Mouse, ViewportBoundsCache Bounds)> _inputProviders = new();
    private ViewId _activeInputViewId = ViewId.Empty;

    // Win32 WM_MOUSEWHEEL interception (independant du hit-testing WPF/D3D11).
    private const int WM_MOUSEWHEEL = 0x020A;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    // ---- Events ----

    /// <summary>Fired when the engine has finished initializing and is ready to accept views.</summary>
    public event EventHandler? Started;

    /// <summary>
    /// Static version of <see cref="Started"/>: fired by the <see cref="Instance"/> as soon
    /// as it finishes loading. Controls that are constructed before the host is loaded
    /// can subscribe here and be notified whenever the shared engine becomes ready.
    /// </summary>
    public static event EventHandler? InstanceStarted;

    /// <summary>
    /// Fired after every <see cref="Draw"/> call.
    /// <c>ViewportControl</c> instances subscribe to copy their view's
    /// <see cref="RenderTargetSurface.Texture"/> into their WPF-visible surface.
    /// </summary>
    public event EventHandler<GameTime>? FrameReady;

    // ---- Public API ----

    /// <summary>The shared engine game instance (available after <see cref="Started"/> fires).</summary>
    public CasaEngineGame? Game => _game;

    /// <summary>Whether the engine has finished loading and <see cref="Started"/> has already fired.</summary>
    public bool IsStarted => _initialized;

    /// <summary>Shortcut to the ViewManager of the shared game.</summary>
    public ViewManager? ViewManager => _game?.GameManager.ViewManager;

    // ---- WpfGame overrides ----

    protected override bool CanRender => _initialized;

    protected override void Initialize()
    {
        _instance = this;

        // Wrap this host's (shared) GraphicsDevice in a service so CasaEngineGame
        // can register it without creating a new GraphicsDeviceManager.
        var service = new WpfGraphicsDeviceService(this);
        _game = new CasaEngineGame(null, service);
        _game.IsRunningInGameEditorMode = true;

        // InitializeWithEditor() must come first: it calls CasaEngineGame.Initialize()
        // which creates InputComponent. SetInputProvider() accesses InputComponent, so it
        // must be called *after* InitializeWithEditor().
        _game.InitializeWithEditor();

        // Wire default WPF input providers scoped to this EngineHost control.
        // PR 6 replaces this with per-view routing via InputRouter.InjectMouseState().
        _game.SetInputProvider(
            new KeyboardStateProvider(new WpfKeyboard(this)),
            new MouseStateProvider(new WpfMouse(this)));

        base.Initialize();

        // Intercept WM_MOUSEWHEEL before WPF routing — reliable even when D3D11Host
        // bypasses hit-testing and WPF's MouseWheel event never fires.
        ComponentDispatcher.ThreadPreprocessMessage += OnWheelMessage;
    }

    protected override void LoadContent()
    {
        _game!.LoadContentWithEditor();
        _initialized = true;

        // Disable physics in editor mode (same as GameEditor).
        _game.PhysicsEngineComponent.Enabled = false;

        Started?.Invoke(this, EventArgs.Empty);
        InstanceStarted?.Invoke(this, EventArgs.Empty);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_initialized)
        {
            _game!.UpdateWithEditor(gameTime);

            // ---- Cursor-based input dispatch (100 % Win32, game-thread safe) ----
            // Determine which viewport the cursor is over and switch providers + active
            // view automatically every frame. This replaces the broken MouseEnter approach.
            if (GetCursorPos(out var pt))
            {
                bool overAnyViewport = false;
                foreach (var (viewId, (kbd, mouse, bounds)) in _inputProviders)
                {
                    if (bounds.Contains(pt.X, pt.Y))
                    {
                        overAnyViewport = true;
                        if (viewId != _activeInputViewId)
                        {
                            _activeInputViewId = viewId;
                            _game.SetInputProvider(kbd, mouse);
                            Logs.WriteDebug($"[InputDiag] Active viewport switched to {viewId}");
                            if (ViewManager != null &&
                                ViewManager.TryGetView(viewId, out var v))
                                ViewManager.SetActive(v);
                            // Update IsActiveViewport so only the hovered viewport's
                            // GizmoComponent reacts to mouse clicks.
                            foreach (var (vid, vctx) in _viewContexts)
                            {
                                if (vctx.Gizmo != null)
                                    vctx.Gizmo.IsActiveViewport = (vid == viewId);
                            }
                        }
                        break;
                    }
                }

                // Cursor left all viewports → deactivate gizmo input so clicks outside
                // the viewport are never forwarded to GizmoComponent or camera scripts.
                if (!overAnyViewport && !_activeInputViewId.IsEmpty)
                {
                    Logs.WriteDebug($"[InputDiag] Cursor left all viewports, deactivating input");
                    _activeInputViewId = ViewId.Empty;
                    foreach (var (_, vctx) in _viewContexts)
                    {
                        if (vctx.Gizmo != null)
                            vctx.Gizmo.IsActiveViewport = false;
                    }
                }
            }

            // Met à jour uniquement la caméra du viewport actif (celui survolé par la souris).
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var activeCtx = _game.GameManager.ViewManager.ActiveView?.Tag as EditorViewContext;
            activeCtx?.CameraEntity?.Update(dt);
            activeCtx?.CameraEntity?.GameplayProxy?.Update(dt);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_initialized)
        {
            _game!.DrawWithEditor(gameTime);
            FrameReady?.Invoke(this, gameTime);
        }
    }

    /// <summary>
    /// Switches the shared <see cref="InputComponent"/> to read keyboard and mouse
    /// state from the specified WPF controls.
    /// Called by <see cref="ViewportControl"/> on <c>MouseEnter</c> so that
    /// <c>ScriptArcBallCamera</c> and other navigation scripts always consume
    /// events from the hovered viewport rather than the EngineHost root element.
    /// </summary>
    /// Registers per-viewport input providers. Called by ViewportControl.Initialize().
    /// The cursor dispatch in Update() switches providers automatically each frame.
    /// </summary>
    private void OnWheelMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message != WM_MOUSEWHEEL) return;
        int delta   = (short)(((uint)msg.wParam) >> 16);
        int screenX = (short)((uint)msg.lParam & 0xFFFF);
        int screenY = (short)(((uint)msg.lParam >> 16) & 0xFFFF);
        foreach (var (_, (_, _, bounds)) in _inputProviders)
        {
            if (bounds.Contains(screenX, screenY))
            {
                bounds.AddScrollDelta(delta);
                break;
            }
        }
    }

    internal void SetActiveViewportInput(ViewId viewId, RawKeyboardProvider keyboard, RawMouseProvider mouse, ViewportBoundsCache bounds)
    {
        Logs.WriteDebug($"[InputDiag] EngineHost.RegisterViewportInput viewId={viewId} gameReady={_game != null}");
        _inputProviders[viewId] = (keyboard, mouse, bounds);
        // Seed the game with a valid provider immediately (will be overwritten by Update dispatch).
        if (_game != null && _inputProviders.Count == 1)
            _game.SetInputProvider(keyboard, mouse);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnWheelMessage;

            foreach (var ctx in _viewContexts.Values)
            {
                UnregisterEditorViewInternal(ctx);
            }

            _viewContexts.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }

    // ---- View registry ----

    /// <summary>
    /// Creates a new editor viewport from <paramref name="def"/> and registers it
    /// with the engine's <see cref="ViewManager"/>.
    /// </summary>
    /// <returns>
    /// A stable <see cref="ViewId"/> that <c>ViewportControl</c> and other editor
    /// code should store to retrieve the view's data.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called before the host has finished initializing.
    /// Subscribe to <see cref="Started"/> and call from there.
    /// </exception>
    public ViewId RegisterEditorView(EditorViewDefinition def)
    {
        if (_game == null || !_initialized)
        {
            throw new InvalidOperationException(
                $"{nameof(RegisterEditorView)} must be called after {nameof(Started)} fires.");
        }

        // ---- 1. World ----
        var world = def.World ?? new Framework.World.World();
        if (def.World == null)
        {
            // New empty world: must be content-loaded before entities are added.
            world.LoadContent(_game);
        }

        // ---- 2. Camera ----
        // Create a dedicated camera entity per view so each view has an independent transform.
        var cameraEntity = new Entity { Name = $"Camera_{def.Name}", IsVisible = false };
        CameraComponent camera;

        bool is2D = def.ViewType is EditorViewType.Sprite
                                 or EditorViewType.Animation2d
                                 or EditorViewType.TileMap;

        if (is2D)
        {
            var cam2d = new Camera3dIn2dAxisComponent
            {
                Target = new Vector3(def.InitialWidth / 2f, def.InitialHeight / 2f, 0f),
            };
            camera = cam2d;
        }
        else
        {
            var cam3d = new ArcBallCameraComponent();
            cam3d.SetCamera(
                Vector3.Backward * 10 + Vector3.Up * 10,
                Vector3.Zero,
                Vector3.Up);
            cameraEntity.GameplayProxyClassName = nameof(ScriptArcBallCamera);
            camera = cam3d;
        }

        cameraEntity.AddComponent(camera);
        cameraEntity.Initialize();
        // In EDITOR builds, Entity.Initialize() and Entity.InitializeWithWorld() skip
        // GameplayProxy.Initialize / InitializeWithWorld (guarded by #if !EDITOR in Entity.cs).
        // Call them explicitly so ScriptArcBallCamera can fetch _inputComponent.
        cameraEntity.GameplayProxy?.Initialize(cameraEntity);
        cameraEntity.InitializeWithWorld(world);
        cameraEntity.GameplayProxy?.InitializeWithWorld(world);

        // ---- 3. Render-target surface ----
        int w = Math.Max(def.InitialWidth, 1);
        int h = Math.Max(def.InitialHeight, 1);
        var surface = new RenderTargetSurface(base.GraphicsDevice, w, h);

        // ---- 4. Register in ViewManager ----
        // Choose update mode: 3-D world views are real-time (camera navigation)
        // while 2-D asset previews are on-demand (re-render only on content change).
        var updateMode = def.UpdateMode ?? (is2D ? ViewUpdateMode.OnDemand : ViewUpdateMode.RealTime);

        var viewDef = new ViewDefinition
        {
            Name       = def.Name,
            World      = world,
            Camera     = camera,
            Surface    = surface,
            ClearColor = def.ClearColor,
            UpdateMode = updateMode,
        };

        var viewId = _game.GameManager.ViewManager.CreateView(viewDef);
        _game.GameManager.ViewManager.TryGetView(viewId, out var renderView);

        // ---- 5. EditorViewContext ----
        var ctx = new EditorViewContext(viewId, renderView!, def.Name, def.ViewType)
        {
            World        = world,
            Camera       = camera,
            CameraEntity = cameraEntity,
            Surface      = surface,
        };

        // ---- 6. Optional editor overlays ----
        // NOTE: In PR 2–4 these are still DrawableGameComponent instances added to
        //       Game.Components and rendered globally.  PR 5 extracts them into
        //       per-view standalone objects driven by EditorViewPipeline.
        if (!is2D)
        {
            // Explicitly call Initialize() immediately after creating each component.
            // MonoGame may defer OnComponentAdded→Initialize() to the next Update tick,
            // so GizmoComponent.Gizmo would still be null when RegisterEditorView returns.
            if (def.ShowGizmo) { ctx.Gizmo = new GizmoComponent(_game); ctx.Gizmo.Initialize(); }
            if (def.ShowGrid)  { ctx.Grid  = new GridComponent(_game);  ctx.Grid.Initialize();  }
            if (def.ShowAxis)  { ctx.Axis  = new AxisComponent(_game);  ctx.Axis.Initialize();  }
        }

        // ---- 7. Wire EditorViewPipeline for per-view overlay rendering ----
        // Each overlay component has Visible=false so it won't draw in Phase 3
        // of DrawWithEditor.  Instead, EditorViewPipeline calls DrawForView()
        // during Phase 2 while the correct per-view render target is active.
        if (def.ShowGizmo || def.ShowGrid || def.ShowAxis)
        {
            var pipeline = new EditorViewPipeline();

            if (def.ShowGrid && ctx.Grid != null)
            {
                var grid = ctx.Grid;
                pipeline.RenderGridAction = (gd, _, frame) => grid.DrawForView(gd, in frame);
            }

            if (def.ShowGizmo && ctx.Gizmo != null)
            {
                var gizmo = ctx.Gizmo;
                gizmo.ActiveCamera  = ctx.Camera;   // bind camera for per-view Update()
                gizmo.ActiveSurface = ctx.Surface;  // bind RT surface for correct Unproject
                pipeline.RenderGizmosAction = (_, _, frame) => gizmo.DrawForView(in frame);
            }

            if (def.ShowAxis && ctx.Axis != null)
            {
                var axis = ctx.Axis;
                pipeline.RenderAxisAction = (gd, _, frame) => axis.DrawForView(gd, in frame);
            }

            renderView!.Pipeline = pipeline;
        }

        _viewContexts[viewId] = ctx;
        return viewId;
    }

    /// <summary>
    /// Removes a view created by <see cref="RegisterEditorView"/> and disposes all
    /// associated resources (surface, overlay components).
    /// </summary>
    public void UnregisterEditorView(ViewId viewId)
    {
        if (!_viewContexts.TryGetValue(viewId, out var ctx)) return;

        UnregisterEditorViewInternal(ctx);
        _viewContexts.Remove(viewId);
    }

    /// <summary>
    /// Returns the <see cref="EditorViewContext"/> for <paramref name="viewId"/>,
    /// or <see langword="null"/> if the view was not registered via this host.
    /// </summary>
    public EditorViewContext? GetViewContext(ViewId viewId)
        => _viewContexts.TryGetValue(viewId, out var ctx) ? ctx : null;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="viewId"/> was registered
    /// with this host.
    /// </summary>
    public bool HasView(ViewId viewId) => _viewContexts.ContainsKey(viewId);

    // ---- Helpers ----

    private void UnregisterEditorViewInternal(EditorViewContext ctx)
    {
        if (_game == null) return;

        // Clean up per-viewport input provider registration.
        _inputProviders.Remove(ctx.ViewId);
        if (_activeInputViewId == ctx.ViewId)
            _activeInputViewId = ViewId.Empty;

        // Remove overlay components from the game component list.
        if (ctx.Gizmo != null)
        {
            _game.Components.Remove(ctx.Gizmo);
            ctx.Gizmo = null;
        }

        if (ctx.Grid != null)
        {
            _game.Components.Remove(ctx.Grid);
            ctx.Grid = null;
        }

        if (ctx.Axis != null)
        {
            _game.Components.Remove(ctx.Axis);
            ctx.Axis = null;
        }

        // Remove the view from the ViewManager.
        if (_game.GameManager.ViewManager.TryGetView(ctx.ViewId, out var view))
        {
            _game.GameManager.ViewManager.Remove(view);
        }

        // Dispose the context (frees the RenderTargetSurface).
        ctx.Dispose();
    }
}

#endif
