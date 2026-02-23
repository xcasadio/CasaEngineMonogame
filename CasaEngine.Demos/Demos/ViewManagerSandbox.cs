using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Comprehensive sandbox demo for the ViewManager v2 system.
///
/// Features demonstrated:
/// <list type="bullet">
///   <item>4-camera split screen (Grid4 layout) on launch.</item>
///   <item>Dynamic view creation/removal (Tab = add, Backspace = remove last).</item>
///   <item>UpdateMode cycling per view (F6..F9 = cycle mode on view 0..3).</item>
///   <item>Debug overlay toggle (F5).</item>
///   <item>OnDemand invalidation (Space = invalidate all OnDemand views).</item>
///   <item>RenderTargetPool leak check — RT count stays stable after add/remove cycles.</item>
///   <item>HUD showing view count, RT pool stats, per-view modes.</item>
/// </list>
///
/// Navigation: use the MGUI demo navigator panel (top-right) to switch demos.
///             Press F1 to toggle the panel visibility.
///
/// Controls (while this demo is active):
/// <code>
/// Tab       — add a new view (up to 4)
/// Backspace — remove the last view
/// F6..F9    — cycle UpdateMode on view 1..4
/// F5        — toggle debug overlay on view 1
/// Space     — Invalidate() all OnDemand views
/// </code>
/// </summary>
public class ViewManagerSandbox : Demo
{
    private const int MaxViews = 4;

    private readonly List<ArcBallCameraComponent> _cameras       = new();
    private readonly List<RenderTargetSurface?>   _rtSurfaces    = new();
    private CasaEngineGame?                        _game;
    private SpriteBatch?                           _spriteBatch;

    // Keyboard debounce
    private KeyboardState _prevKb;

    // HUD
    private float _fpsAccum;
    private int   _fpsSamples;
    private float _displayFps;

    public override string Title => "ViewManager v2 Sandbox";
    public override string Description => "Comprehensive sandbox for the ViewManager v2 system: multi-view split screen, dynamic add/remove views, UpdateMode cycling (RealTime/Throttled/OnDemand), and debug overlays.";

    // ---- Demo lifecycle ----

    public override void Initialize(CasaEngineGame game)
    {
        _game        = game;
        _spriteBatch = game.SpriteBatch;

        var world = game.GameManager.CurrentWorld;

        // Ground plane
        var ground = new Entity { Name = "Ground" };
        var gm     = new StaticMeshComponent();
        ground.RootComponent = gm;
        gm.Mesh = StaticMesh.CreateFromGeometricPrimitive(new BoxPrimitive(30, 1, 30));
        gm.Mesh.Initialize(game.AssetContentManager);
        gm.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(ground);

        // A ring of coloured boxes so different camera angles see something distinct
        var colors = new[] { Color.Tomato, Color.SkyBlue, Color.LimeGreen, Color.Gold };
        for (int i = 0; i < 8; i++)
        {
            float angle = MathF.PI * 2f * i / 8f;
            var box = new Entity { Name = $"Box {i}" };
            var bm  = new StaticMeshComponent();
            box.RootComponent = bm;
            bm.Mesh = StaticMesh.CreateFromGeometricPrimitive(new BoxPrimitive(1.5f, 2f, 1.5f));
            bm.Mesh.Initialize(game.AssetContentManager);
            bm.LocalPosition = new Vector3(MathF.Cos(angle) * 8f, 1f, MathF.Sin(angle) * 8f);
            world.AddEntity(box);
        }
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        // Create 4 cameras with different positions around the scene
        var positions = new[]
        {
            (Vector3.Backward * 20 + Vector3.Up * 14, "Cam Front"),
            (Vector3.Right    * 20 + Vector3.Up * 14, "Cam Right"),
            (Vector3.Up       * 30 + Vector3.Forward * 0.01f, "Cam Top"),
            (Vector3.Left     * 20 + Vector3.Up * 14, "Cam Left"),
        };

        _cameras.Clear();
        CameraComponent? firstCamera = null;

        for (int i = 0; i < MaxViews; i++)
        {
            var entity = new Entity { Name = positions[i].Item2 };
            var cam    = new ArcBallCameraComponent();
            entity.RootComponent = cam;
            entity.Initialize();
            game.GameManager.CurrentWorld.AddEntity(entity);
            _cameras.Add(cam);

            if (firstCamera == null) firstCamera = cam;
        }

        return firstCamera!;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        var game = _game!;
        var pp   = game.GraphicsDevice.PresentationParameters;
        var rects = SplitScreenLayout.Compute(pp.BackBufferWidth, pp.BackBufferHeight, 4, SplitMode.Grid4);

        var targets = new[]
        {
            (Vector3.Backward * 20 + Vector3.Up * 14, Vector3.Zero),
            (Vector3.Right    * 20 + Vector3.Up * 14, Vector3.Zero),
            (Vector3.Up       * 30 + Vector3.Forward, Vector3.Zero),
            (Vector3.Left     * 20 + Vector3.Up * 14, Vector3.Zero),
        };

        var world       = game.GameManager.CurrentWorld;
        var viewManager = game.GameManager.ViewManager;
        viewManager.Clear();
        viewManager.AutoLayoutMode = SplitMode.Grid4;
        _rtSurfaces.Clear();

        var clearColors = new[]
        {
            Color.CornflowerBlue,
            new Color(0.12f, 0.12f, 0.20f),
            new Color(0.10f, 0.20f, 0.10f),
            new Color(0.20f, 0.10f, 0.10f),
        };

        for (int i = 0; i < MaxViews; i++)
        {
            _cameras[i].SetCamera(targets[i].Item1, targets[i].Item2, Vector3.Up);
            _cameras[i].OnScreenResized(rects[i].Width, rects[i].Height);

            var view = new RenderView(world, _cameras[i], new BackBufferSurface(rects[i]))
            {
                Name       = $"View {i + 1} ({_cameras[i].Owner?.Name})",
                ClearColor = clearColors[i],
                UpdateMode = i switch
                {
                    0 => ViewUpdateMode.RealTime,
                    1 => ViewUpdateMode.Throttled,
                    2 => ViewUpdateMode.OnDemand,
                    _ => ViewUpdateMode.RealTime,
                },
                TargetFrameRate = 5f,   // Throttled view: 5 fps
            };

            // Mark OnDemand view as dirty so it renders at least once
            if (view.UpdateMode == ViewUpdateMode.OnDemand)
            {
                view.Invalidate();
            }

            viewManager.Add(view);
            _rtSurfaces.Add(null); // No RT surface for backbuffer views
        }
    }



    public override void Update(GameTime gameTime)
    {
        var kb  = Keyboard.GetState();
        var vm  = _game!.GameManager.ViewManager;
        var world = _game.GameManager.CurrentWorld;

        // ---- Add view (Tab) ----
        if (kb.IsKeyDown(Keys.Tab) && !_prevKb.IsKeyDown(Keys.Tab))
        {
            var views = vm.Views;
            if (views.Count < MaxViews)
            {
                var pp = _game.GraphicsDevice.PresentationParameters;

                // Add new view (surface rect will be set by ApplyBackBufferLayout below)
                var camIdx = views.Count;
                if (camIdx < _cameras.Count)
                {
                    var newView = new RenderView(world, _cameras[camIdx], new BackBufferSurface(Rectangle.Empty))
                    {
                        Name       = $"View {camIdx + 1} (dynamic)",
                        ClearColor = new Color(0.3f, 0.15f, 0.3f),
                        UpdateMode = ViewUpdateMode.RealTime,
                    };
                    vm.Add(newView);
                    _rtSurfaces.Add(null);
                }

                // Update layout mode and recompute all viewport rects
                vm.AutoLayoutMode = vm.Views.Count == MaxViews ? SplitMode.Grid4 : SplitMode.Vertical;
                vm.ApplyBackBufferLayout(pp.BackBufferWidth, pp.BackBufferHeight);
            }
        }

        // ---- Remove last view (Backspace) ----
        if (kb.IsKeyDown(Keys.Back) && !_prevKb.IsKeyDown(Keys.Back))
        {
            var views = vm.Views;
            if (views.Count > 1)
            {
                vm.Remove(views[^1]);
                if (_rtSurfaces.Count > 0) _rtSurfaces.RemoveAt(_rtSurfaces.Count - 1);

                // Update layout mode and recompute all viewport rects
                var pp = _game.GraphicsDevice.PresentationParameters;
                vm.AutoLayoutMode = vm.Views.Count == MaxViews ? SplitMode.Grid4 : SplitMode.Vertical;
                vm.ApplyBackBufferLayout(pp.BackBufferWidth, pp.BackBufferHeight);
            }
        }

        // ---- Cycle UpdateMode on view N (F6..F9) ----
        var fKeys = new[] { Keys.F6, Keys.F7, Keys.F8, Keys.F9 };
        var allViews = vm.Views;
        for (int i = 0; i < fKeys.Length && i < allViews.Count; i++)
        {
            if (kb.IsKeyDown(fKeys[i]) && !_prevKb.IsKeyDown(fKeys[i]))
            {
                var v = allViews[i];
                v.UpdateMode = v.UpdateMode switch
                {
                    ViewUpdateMode.RealTime  => ViewUpdateMode.Throttled,
                    ViewUpdateMode.Throttled => ViewUpdateMode.OnDemand,
                    ViewUpdateMode.OnDemand  => ViewUpdateMode.RealTime,
                    _                        => ViewUpdateMode.RealTime,
                };

                if (v.UpdateMode == ViewUpdateMode.OnDemand)
                {
                    v.Invalidate();
                }
            }
        }

        // ---- Toggle debug overlay on view 1 (F5) ----
        if (kb.IsKeyDown(Keys.F5) && !_prevKb.IsKeyDown(Keys.F5) && allViews.Count > 0)
        {
            allViews[0].ShowDebugOverlay = !allViews[0].ShowDebugOverlay;
        }

        // ---- Invalidate all OnDemand views (Space) ----
        if (kb.IsKeyDown(Keys.Space) && !_prevKb.IsKeyDown(Keys.Space))
        {
            foreach (var v in allViews)
            {
                if (v.UpdateMode == ViewUpdateMode.OnDemand)
                {
                    v.Invalidate();
                }
            }
        }

        _prevKb = kb;

        // FPS tracking
        _fpsAccum   += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _fpsSamples++;
        if (_fpsAccum >= 0.5f)
        {
            _displayFps = _fpsSamples / _fpsAccum;
            _fpsAccum   = 0f;
            _fpsSamples = 0;
        }
    }

    /// <summary>
    /// Draws the HUD overlay after the render pipeline has completed.
    /// </summary>
    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        if (_spriteBatch == null) return;

        var vm   = game.GameManager.ViewManager;
        var pool = RenderTargetPool.Shared;
        var pp   = game.GraphicsDevice.PresentationParameters;

        // Build HUD string
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"=== ViewManager v2 Sandbox ===");
        lines.AppendLine($"FPS: {_displayFps:F1}");
        lines.AppendLine($"Views: {vm.Views.Count} / {MaxViews}");
        if (pool != null)
        {
            lines.AppendLine($"RT pool: {pool.TotalCount - pool.FreeCount} active, {pool.FreeCount} free, {pool.TotalCount} total");
        }
        lines.AppendLine();
        lines.AppendLine("Per-view modes:");
        for (int i = 0; i < vm.Views.Count; i++)
        {
            var v = vm.Views[i];
            string mode = v.UpdateMode switch
            {
                ViewUpdateMode.RealTime  => "RealTime",
                ViewUpdateMode.Throttled => $"Throttled({v.TargetFrameRate:F0}fps)",
                ViewUpdateMode.OnDemand  => $"OnDemand[dirty={v.IsDirty}]",
                _ => "?"
            };
            lines.AppendLine($"  V{i + 1}: {mode}  overlay={v.ShowDebugOverlay}");
        }
        lines.AppendLine();
        lines.AppendLine("[Tab]=AddView  [Bksp]=Remove");
        lines.AppendLine("[F6-F9]=CycleMode  [F5]=Overlay");
        lines.AppendLine("[Space]=InvalidateOnDemand");

        // Render HUD in the top-left corner of the first view
        var font = game.FontSystem.GetFont(14);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            null, null, null);
        font.DrawText(_spriteBatch, lines.ToString(), new Vector2(10, 10), Color.White);
        _spriteBatch.End();
    }

    public override void Clean()
    {
        // Dispose any RT surfaces (returns to pool)
        foreach (var rt in _rtSurfaces)
        {
            rt?.Dispose();
        }

        _rtSurfaces.Clear();
        _cameras.Clear();

        _game?.GameManager.ViewManager.Clear();
        _game = null;
    }
}
