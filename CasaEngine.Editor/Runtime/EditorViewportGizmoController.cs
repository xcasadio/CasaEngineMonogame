using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game.Components.DebugTools;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Transform;
using CasaEngine.Framework.World;
using GizmoTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Runtime;

internal sealed class EditorViewportGizmoController : IDisposable
{
    private readonly HostedEditorGameAdapter _editorRuntime;
    private TransformGizmoComponent? _gizmo;
    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;

    public EditorViewportGizmoController(HostedEditorGameAdapter editorRuntime)
    {
        _editorRuntime = editorRuntime;
    }

    public event Action<Entity?>? SelectedEntityChanged;

    public void EnsureInitialized(RenderView? renderView, ArcBallCameraComponent? camera, RenderTargetSurface? surface, World world)
    {
        if (renderView == null || camera == null || surface == null)
        {
            return;
        }

        if (_gizmo == null)
        {
            _gizmo = new TransformGizmoComponent(_editorRuntime);
            _gizmo.Initialize();
            _gizmo.SelectionChanged += OnGizmoSelectionChanged;
        }

        _gizmo.ActiveCamera = camera;
        _gizmo.ActiveSurface = surface;
        _gizmo.SelectionWorld = world;
        _gizmo.IsActiveViewport = true;
        _gizmo.SetSelectionPool(GetViewportSelectableObjects(world));

        var overlayPipeline = renderView.Pipeline as OverlayViewPipeline ?? new OverlayViewPipeline();
        overlayPipeline.RenderGizmosAction = (_, _, frame) => _gizmo.DrawForView(in frame);
        renderView.Pipeline = overlayPipeline;
    }

    public void Synchronize(ArcBallCameraComponent? camera, RenderTargetSurface? surface, World? world)
    {
        if (_gizmo == null)
        {
            return;
        }

        _gizmo.ActiveCamera = camera;
        _gizmo.ActiveSurface = surface;
        _gizmo.SelectionWorld = world;
        RefreshPresentation();
    }

    public void ResetWorld(World world)
    {
        if (_gizmo == null)
        {
            return;
        }

        _gizmo.SelectionWorld = world;
        _gizmo.ClearSelection();
        _gizmo.SetSelectionPool(GetViewportSelectableObjects(world));
        RefreshPresentation();
    }

    public void RefreshWorldSelection(World world, Entity? selectedEntity)
    {
        if (_gizmo == null)
        {
            return;
        }

        _gizmo.SelectionWorld = world;
        _gizmo.SetSelectionPool(GetViewportSelectableObjects(world));
        _gizmo.ClearSelection();

        if (selectedEntity?.RootComponent != null)
        {
            _gizmo.AddToSelection(selectedEntity.RootComponent);
        }

        RefreshPresentation();
    }

    public void Deactivate()
    {
        if (_gizmo != null)
        {
            _gizmo.IsActiveViewport = false;
        }
    }

    public void SetSelectedEntity(Entity? entity)
    {
        if (_gizmo == null)
        {
            return;
        }

        _gizmo.ClearSelection();

        if (entity?.RootComponent != null)
        {
            _gizmo.AddToSelection(entity.RootComponent);
        }

        RefreshPresentation();
    }

    public void Update(
        GameTime gameTime,
        ViewInputContext inputContext,
        bool receivesInput,
        bool isKeyboardFocused,
        ArcBallCameraComponent? camera,
        RenderTargetSurface? surface,
        World? world)
    {
        if (_gizmo == null || camera == null || surface == null)
        {
            _previousKeyboardState = inputContext.KeyboardState;
            _previousMouseState = inputContext.MouseState;
            return;
        }

        _gizmo.ActiveCamera = camera;
        _gizmo.ActiveSurface = surface;
        _gizmo.SelectionWorld = world;
        _gizmo.Gizmo.ActiveViewport = new Viewport(0, 0, surface.ViewportRect.Width, surface.ViewportRect.Height);
        _gizmo.Gizmo.UpdateCameraProperties(camera.ViewMatrix, camera.ProjectionMatrix, camera.Position);

        if (!receivesInput && !isKeyboardFocused)
        {
            _gizmo.IsActiveViewport = false;
            _gizmo.Gizmo.RefreshPresentation();
            _previousKeyboardState = inputContext.KeyboardState;
            _previousMouseState = inputContext.MouseState;
            return;
        }

        var keyboardState = inputContext.KeyboardState;
        var mouseState = inputContext.MouseState;

        _gizmo.IsActiveViewport = receivesInput;

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D1))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.Translate;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D2))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.Rotate;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D3))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.NonUniformScale;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D4))
        {
            _gizmo.Gizmo.ActiveMode = GizmoMode.UniformScale;
        }

        _gizmo.Gizmo.PrecisionModeEnabled = isKeyboardFocused && (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.O))
        {
            _gizmo.Gizmo.ToggleActiveSpace();
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.I))
        {
            _gizmo.Gizmo.SnapEnabled = !_gizmo.Gizmo.SnapEnabled;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.P))
        {
            _gizmo.Gizmo.NextPivotType();
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.Escape))
        {
            _gizmo.Gizmo.Clear();
        }

        if (!receivesInput)
        {
            _gizmo.Gizmo.RefreshPresentation();
            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
            return;
        }

        bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed
            && _previousMouseState.LeftButton == ButtonState.Released;

        if (leftJustPressed)
        {
            bool addToSelection = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            bool removeFromSelection = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
            _gizmo.Gizmo.SelectEntities(new Vector2(mouseState.X, mouseState.Y), addToSelection, removeFromSelection);
        }

        _gizmo.Gizmo.Update(gameTime, keyboardState, mouseState);

        _previousKeyboardState = keyboardState;
        _previousMouseState = mouseState;
    }

    public void Dispose()
    {
        if (_gizmo != null)
        {
            _gizmo.SelectionChanged -= OnGizmoSelectionChanged;
            _gizmo.ClearSelection();
            _editorRuntime.Components.Remove(_gizmo);
            _gizmo.Dispose();
            _gizmo = null;
        }
    }

    private void OnGizmoSelectionChanged(object? sender, List<ITransformableObject> selection)
    {
        var selectedEntity = selection
            .OfType<EntityComponent>()
            .Select(component => component.Owner)
            .FirstOrDefault(owner => owner != null);

        SelectedEntityChanged?.Invoke(selectedEntity);
    }

    private void RefreshPresentation()
    {
        if (_gizmo?.ActiveCamera != null)
        {
            _gizmo.Gizmo.UpdateCameraProperties(
                _gizmo.ActiveCamera.ViewMatrix,
                _gizmo.ActiveCamera.ProjectionMatrix,
                _gizmo.ActiveCamera.Position);
        }

        if (_gizmo?.ActiveSurface != null)
        {
            var viewportRect = _gizmo.ActiveSurface.ViewportRect;
            _gizmo.Gizmo.ActiveViewport = new Viewport(0, 0, viewportRect.Width, viewportRect.Height);
        }

        _gizmo?.Gizmo.RefreshPresentation();
    }

    private bool IsNewKeyPress(KeyboardState keyboardState, Keys key)
    {
        return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private static IEnumerable<ITransformableObject> GetViewportSelectableObjects(World world)
    {
        var selectables = new List<ITransformableObject>();

        foreach (var entity in world.Entities)
        {
            AddSelectableRoots(entity, selectables);
        }

        return selectables;
    }

    private static void AddSelectableRoots(Entity entity, List<ITransformableObject> selectables)
    {
        if (entity.RootComponent != null)
        {
            selectables.Add(entity.RootComponent);
        }

        foreach (var child in entity.Children)
        {
            AddSelectableRoots(child, selectables);
        }
    }
}