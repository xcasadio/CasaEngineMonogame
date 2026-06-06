using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application.Components.DebugTools;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Transform;
using CasaEngine.Framework.Scene.World;
using GizmoTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Editor.Runtime;

internal sealed class EditorViewportGizmoController : IDisposable
{
    private readonly record struct TransformSnapshot(ITransformableObject Transformable, Vector3 Position, Quaternion Orientation, Vector3 Scale);
    private static readonly EditorHistoryContext DefaultHistoryContext = new(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport);

    private sealed class ManipulationSession
    {
        public ManipulationSession(string description, List<TransformSnapshot> initialSnapshots)
        {
            Description = description;
            InitialSnapshots = initialSnapshots;
        }

        public string Description { get; }

        public List<TransformSnapshot> InitialSnapshots { get; }
    }

    private readonly HostedEditorGameAdapter _editorRuntime;
    private TransformGizmoComponent? _gizmo;
    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;
    private bool _suppressSelectionChanged;
    private ManipulationSession? _activeManipulation;
    private GizmoMode _activeMode = GizmoMode.Translate;
    private TransformSpace _activeSpace = TransformSpace.World;

    public EditorViewportGizmoController(HostedEditorGameAdapter editorRuntime)
    {
        _editorRuntime = editorRuntime;
    }

    public bool AllowSelectionPicking { get; set; } = true;

    public bool AllowDeleteSelection { get; set; } = true;

    public EditorHistoryContext HistoryContext { get; set; } = DefaultHistoryContext;

    public GizmoMode ActiveMode
    {
        get => _activeMode;
        set
        {
            if (_activeMode == value)
            {
                return;
            }

            _activeMode = value;
            if (_gizmo?.Gizmo != null && _gizmo.Gizmo.ActiveMode != value)
            {
                _gizmo.Gizmo.ActiveMode = value;
            }

            ActiveModeChanged?.Invoke(value);
        }
    }

    public TransformSpace ActiveSpace
    {
        get => _activeSpace;
        set
        {
            if (_activeSpace == value)
            {
                return;
            }

            _activeSpace = value;
            if (_gizmo?.Gizmo != null && _gizmo.Gizmo.ActiveSpace != value)
            {
                _gizmo.Gizmo.ActiveSpace = value;
            }

            ActiveSpaceChanged?.Invoke(value);
        }
    }

    public event Action<Entity?>? SelectedEntityChanged;
    public event Action<IReadOnlyList<Entity>>? DeleteEntitiesRequested;
    public event Action<GizmoMode>? ActiveModeChanged;
    public event Action<TransformSpace>? ActiveSpaceChanged;

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
            _gizmo.DeleteSelectionEvent += OnGizmoDeleteSelectionChanged;
            _gizmo.Gizmo.GizmoModeChangedEvent += OnGizmoModeChanged;
            _gizmo.Gizmo.TransformSpaceChangedEvent += OnGizmoTransformSpaceChanged;
        }

        _gizmo.ActiveCamera = camera;
        _gizmo.ActiveSurface = surface;
        _gizmo.SelectionWorld = world;
        _gizmo.IsActiveViewport = true;
        _gizmo.Gizmo.ActiveMode = _activeMode;
        _gizmo.Gizmo.ActiveSpace = _activeSpace;
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

        CancelManipulation();
        _gizmo.SelectionWorld = world;
        ApplySelectionUpdate(() =>
        {
            _gizmo.ClearSelection();
            _gizmo.SetSelectionPool(GetViewportSelectableObjects(world));
        });
        RefreshPresentation();
    }

    public void RefreshWorldSelection(World world, Entity? selectedEntity)
    {
        SetSelectedTransformable(selectedEntity?.RootComponent, world);
    }

    public void SetSelectedEntity(Entity? entity)
    {
        SetSelectedTransformable(entity?.RootComponent, entity?.World ?? _gizmo?.SelectionWorld);
    }

    public void SetSelectedTransformable(ITransformableObject? transformable, World? world)
    {
        if (_gizmo == null)
        {
            return;
        }

        CancelManipulation();
        if (world != null)
        {
            _gizmo.SelectionWorld = world;
        }

        ApplySelectionUpdate(() =>
        {
            if (_gizmo.SelectionWorld != null)
            {
                _gizmo.SetSelectionPool(GetViewportSelectableObjects(_gizmo.SelectionWorld, transformable));
            }

            _gizmo.ClearSelection();

            if (transformable != null)
            {
                _gizmo.AddToSelection(transformable);
            }
        });

        RefreshPresentation();
    }

    public void Deactivate()
    {
        if (_gizmo != null)
        {
            _gizmo.IsActiveViewport = false;
        }
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
            CancelManipulation();
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
            CancelManipulation();
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
            ActiveMode = GizmoMode.Translate;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D2))
        {
            ActiveMode = GizmoMode.Rotate;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D3))
        {
            ActiveMode = GizmoMode.NonUniformScale;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.D4))
        {
            ActiveMode = GizmoMode.UniformScale;
        }

        _gizmo.Gizmo.PrecisionModeEnabled = isKeyboardFocused && (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.O))
        {
            ActiveSpace = ActiveSpace == TransformSpace.Local ? TransformSpace.World : TransformSpace.Local;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.I))
        {
            _gizmo.Gizmo.SnapEnabled = !_gizmo.Gizmo.SnapEnabled;
        }

        if (isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.P))
        {
            _gizmo.Gizmo.NextPivotType();
        }

        if (AllowSelectionPicking && isKeyboardFocused && IsNewKeyPress(keyboardState, Keys.Escape))
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
        bool leftJustReleased = mouseState.LeftButton == ButtonState.Released
            && _previousMouseState.LeftButton == ButtonState.Pressed;

        if (leftJustPressed)
        {
            BeginManipulationIfNeeded();

            if (AllowSelectionPicking)
            {
                bool addToSelection = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
                bool removeFromSelection = keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt);
                _gizmo.Gizmo.SelectEntities(new Vector2(mouseState.X, mouseState.Y), addToSelection, removeFromSelection);
            }
        }

        _gizmo.Gizmo.Update(gameTime, keyboardState, mouseState);

        if (leftJustReleased)
        {
            CommitManipulationIfNeeded();
        }

        _previousKeyboardState = keyboardState;
        _previousMouseState = mouseState;
    }

    public void Dispose()
    {
        if (_gizmo != null)
        {
            CancelManipulation();
            _gizmo.Gizmo.TransformSpaceChangedEvent -= OnGizmoTransformSpaceChanged;
            _gizmo.Gizmo.GizmoModeChangedEvent -= OnGizmoModeChanged;
            _gizmo.DeleteSelectionEvent -= OnGizmoDeleteSelectionChanged;
            _gizmo.SelectionChanged -= OnGizmoSelectionChanged;
            _gizmo.ClearSelection();
            _editorRuntime.Components.Remove(_gizmo);
            _gizmo.Dispose();
            _gizmo = null;
        }
    }

    private void OnGizmoSelectionChanged(object? sender, List<ITransformableObject> selection)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        var selectedEntity = selection
            .OfType<EntityComponent>()
            .Select(component => component.Owner)
            .FirstOrDefault(owner => owner != null);

        SelectedEntityChanged?.Invoke(selectedEntity);
    }

    private void OnGizmoDeleteSelectionChanged(object? sender, List<ITransformableObject> selection)
    {
        if (!AllowDeleteSelection)
        {
            return;
        }

        var entities = GetSelectedEntities(selection);
        if (entities.Count == 0)
        {
            return;
        }

        CancelManipulation();
        DeleteEntitiesRequested?.Invoke(entities);
    }

    private void OnGizmoModeChanged(object? sender, EventArgs e)
    {
        if (_gizmo?.Gizmo == null)
        {
            return;
        }

        GizmoMode mode = _gizmo.Gizmo.ActiveMode;
        if (_activeMode == mode)
        {
            return;
        }

        _activeMode = mode;
        ActiveModeChanged?.Invoke(mode);
    }

    private void OnGizmoTransformSpaceChanged(object? sender, EventArgs e)
    {
        if (_gizmo?.Gizmo == null)
        {
            return;
        }

        TransformSpace space = _gizmo.Gizmo.ActiveSpace;
        if (_activeSpace == space)
        {
            return;
        }

        _activeSpace = space;
        ActiveSpaceChanged?.Invoke(space);
    }

    private void ApplySelectionUpdate(Action updateSelection)
    {
        _suppressSelectionChanged = true;
        try
        {
            updateSelection();
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
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

    private void BeginManipulationIfNeeded()
    {
        if (_gizmo == null
            || _gizmo.Gizmo.ActiveAxis == GizmoAxis.None
            || _gizmo.CurrentSelection.Count == 0)
        {
            return;
        }

        _activeManipulation = new ManipulationSession(
            BuildManipulationDescription(_gizmo.Gizmo.ActiveMode, _gizmo.CurrentSelection.Count),
            CaptureSelectionSnapshots(_gizmo.CurrentSelection));
    }

    private void CommitManipulationIfNeeded()
    {
        if (_gizmo == null || _activeManipulation == null)
        {
            return;
        }

        var initialSnapshots = _activeManipulation.InitialSnapshots;
        string description = _activeManipulation.Description;
        var finalSnapshots = CaptureSnapshots(initialSnapshots);
        if (!HasTransformDifference(initialSnapshots, finalSnapshots))
        {
            _activeManipulation = null;
            return;
        }

        var command = new EditorDelegateCommand(
            description,
            () => ApplySnapshots(finalSnapshots),
            () => ApplySnapshots(initialSnapshots));

        _activeManipulation = null;
        EditorHistoryService.Current.Execute(
            HistoryContext.IsEmpty ? DefaultHistoryContext : HistoryContext,
            command);
    }

    private void CancelManipulation()
    {
        _activeManipulation = null;
    }

    private void ApplySnapshots(IReadOnlyList<TransformSnapshot> snapshots)
    {
        for (int index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            snapshot.Transformable.Position = snapshot.Position;
            snapshot.Transformable.Orientation = snapshot.Orientation;
            snapshot.Transformable.Scale = snapshot.Scale;
        }

        RefreshPresentation();
    }

    private static List<TransformSnapshot> CaptureSelectionSnapshots(IReadOnlyList<ITransformableObject> selection)
    {
        var snapshots = new List<TransformSnapshot>(selection.Count);
        for (int index = 0; index < selection.Count; index++)
        {
            var transformable = selection[index];
            snapshots.Add(new TransformSnapshot(transformable, transformable.Position, transformable.Orientation, transformable.Scale));
        }

        return snapshots;
    }

    private static List<TransformSnapshot> CaptureSnapshots(IReadOnlyList<TransformSnapshot> snapshots)
    {
        var result = new List<TransformSnapshot>(snapshots.Count);
        for (int index = 0; index < snapshots.Count; index++)
        {
            var transformable = snapshots[index].Transformable;
            result.Add(new TransformSnapshot(transformable, transformable.Position, transformable.Orientation, transformable.Scale));
        }

        return result;
    }

    private static bool HasTransformDifference(IReadOnlyList<TransformSnapshot> initialSnapshots, IReadOnlyList<TransformSnapshot> finalSnapshots)
    {
        if (initialSnapshots.Count != finalSnapshots.Count)
        {
            return true;
        }

        for (int index = 0; index < initialSnapshots.Count; index++)
        {
            var initial = initialSnapshots[index];
            var final = finalSnapshots[index];
            if (!Equals(initial.Transformable, final.Transformable)
                || initial.Position != final.Position
                || initial.Orientation != final.Orientation
                || initial.Scale != final.Scale)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildManipulationDescription(GizmoMode mode, int selectionCount)
    {
        string operation = mode switch
        {
            GizmoMode.Rotate => "Rotate",
            GizmoMode.NonUniformScale => "Scale",
            GizmoMode.UniformScale => "Scale",
            _ => "Move",
        };

        return selectionCount == 1
            ? $"{operation} Entity"
            : $"{operation} {selectionCount} Entities";
    }

    private bool IsNewKeyPress(KeyboardState keyboardState, Keys key)
    {
        return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private static List<Entity> GetSelectedEntities(IReadOnlyList<ITransformableObject> selection)
    {
        var entities = new List<Entity>(selection.Count);
        var selectedEntities = new HashSet<Entity>();

        for (int index = 0; index < selection.Count; index++)
        {
            if (selection[index] is not EntityComponent { Owner: { } owner })
            {
                continue;
            }

            if (selectedEntities.Add(owner))
            {
                entities.Add(owner);
            }
        }

        if (entities.Count <= 1)
        {
            return entities;
        }

        var filteredEntities = new List<Entity>(entities.Count);
        for (int index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            if (!HasSelectedAncestor(entity, selectedEntities))
            {
                filteredEntities.Add(entity);
            }
        }

        return filteredEntities;
    }

    private static bool HasSelectedAncestor(Entity entity, HashSet<Entity> selectedEntities)
    {
        for (var parent = entity.Parent; parent != null; parent = parent.Parent)
        {
            if (selectedEntities.Contains(parent))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<ITransformableObject> GetViewportSelectableObjects(World world, ITransformableObject? explicitSelection = null)
    {
        var selectables = new List<ITransformableObject>();

        foreach (var entity in world.Entities)
        {
            AddSelectableRoots(entity, selectables);
        }

        if (explicitSelection != null && !selectables.Contains(explicitSelection))
        {
            selectables.Add(explicitSelection);
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