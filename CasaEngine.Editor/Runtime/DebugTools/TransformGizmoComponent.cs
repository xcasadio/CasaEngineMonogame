using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Transform;
using GizmoTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components.DebugTools;

public class TransformGizmoComponent : DrawableGameComponent
{
    public Gizmo Gizmo { get; private set; }

    private CasaEngineGame? _game;
    private readonly Dictionary<ITransformableObject, GizmoTransformableAdapter> _selectionAdapters = [];

    public event EventHandler<List<ITransformableObject>>? DeleteSelectionEvent;
    public event EventHandler<List<ITransformableObject>>? SelectionChanged;
    public event EventHandler<List<ITransformableObject>>? CopyTriggered;

    public CameraComponent? ActiveCamera { get; set; }

    public RenderTargetSurface? ActiveSurface { get; set; }

    public bool IsActiveViewport { get; set; }

    public Scene.World.World? SelectionWorld { get; set; }

    public TransformGizmoComponent(Game game) : base(game)
    {
        _game = game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Manipulator;
        DrawOrder = (int)ComponentDrawOrder.Manipulator;
    }

    public override void Initialize()
    {
        if (Gizmo != null) return;

        base.Initialize();

        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var arialPath = Path.Combine(fontsDir, "arial.ttf");
        if (File.Exists(arialPath))
        {
            _game?.FontSystem.AddFont(File.ReadAllBytes(arialPath));
        }

        var lineEffect = Game.Content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
        var solidEffect = Game.Content.Load<Effect>("Shaders\\DebugSolidColor").Clone();

        Gizmo = new Gizmo(
            Game.GraphicsDevice,
            lineEffect,
            solidEffect.Clone(),
            solidEffect,
            lineEffect.Clone());

        Gizmo.TranslateEvent += GizmoTranslateEvent;
        Gizmo.RotateEvent += GizmoRotateEvent;
        Gizmo.ScaleEvent += GizmoScaleEvent;
        Gizmo.SelectionChanged += OnGizmoSelectionChanged;
        Gizmo.DeleteSelectionEvent += OnGizmoDeleteSelectionEvent;
        Gizmo.CopyTriggered += OnGizmoCopyTriggered;

        Visible = false;
    }

    public override void Update(GameTime gameTime)
    {
        var selectionWorld = SelectionWorld ?? _game?.GameManager.CurrentWorld;

        if (Gizmo.GetSelectionPool() == null && selectionWorld != null)
        {
            SetSelectionPool(EditorWorldEditingService.GetSelectableComponents(selectionWorld));
        }

        if (Gizmo.GetSelectionPool() == null)
        {
            return;
        }

        var camera = ActiveCamera ?? _game?.GameManager.ViewManager.ActiveView?.Camera;
        if (camera != null)
        {
            Gizmo.UpdateCameraProperties(
                camera.ViewMatrix,
                camera.ProjectionMatrix,
                camera.Position);
        }

        if (ActiveSurface != null)
        {
            var r = ActiveSurface.ViewportRect;
            Gizmo.ActiveViewport = new Viewport(r.X, r.Y, r.Width, r.Height);
        }

        if (!IsActiveViewport)
        {
            Gizmo.RefreshPresentation();
        }

        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
    }

    public void DrawForView(in RenderFrame frame)
    {
        Gizmo.UpdateCameraProperties(frame.View, frame.Projection, frame.CameraPosition);
        Gizmo.Draw();
    }

    public IReadOnlyList<ITransformableObject> CurrentSelection => UnwrapSelection(Gizmo.Selection);

    public void SetSelectionPool(IEnumerable<ITransformableObject> selectables)
    {
        var runtimeSelection = selectables.ToList();
        var adapterSelection = runtimeSelection.Select(GetOrCreateAdapter).Cast<ITransformable>().ToList();
        var liveKeys = runtimeSelection.ToHashSet();

        foreach (var staleKey in _selectionAdapters.Keys.Where(key => !liveKeys.Contains(key)).ToList())
        {
            _selectionAdapters.Remove(staleKey);
        }

        Gizmo.SetSelectionPool(adapterSelection);
    }

    public void ClearSelection()
    {
        Gizmo.Clear();
    }

    public void AddToSelection(ITransformableObject transformable)
    {
        Gizmo.AddToSelection(GetOrCreateAdapter(transformable));
    }

    public void RemoveFromSelection(ITransformableObject transformable)
    {
        if (_selectionAdapters.TryGetValue(transformable, out var adapter))
        {
            Gizmo.RemoveFromSelection(adapter);
        }
    }

    private GizmoTransformableAdapter GetOrCreateAdapter(ITransformableObject transformable)
    {
        if (_selectionAdapters.TryGetValue(transformable, out var adapter))
        {
            return adapter;
        }

        adapter = new GizmoTransformableAdapter(transformable);
        _selectionAdapters.Add(transformable, adapter);
        return adapter;
    }

    private static List<ITransformableObject> UnwrapSelection(IEnumerable<ITransformable> selection)
    {
        return selection
            .OfType<GizmoTransformableAdapter>()
            .Select(adapter => adapter.Transformable)
            .ToList();
    }

    private void OnGizmoSelectionChanged(object? sender, List<ITransformable> selection)
    {
        SelectionChanged?.Invoke(this, UnwrapSelection(selection));
    }

    private void OnGizmoDeleteSelectionEvent(object? sender, List<ITransformable> selection)
    {
        DeleteSelectionEvent?.Invoke(this, UnwrapSelection(selection));
    }

    private void OnGizmoCopyTriggered(object? sender, List<ITransformable> selection)
    {
        CopyTriggered?.Invoke(this, UnwrapSelection(selection));
    }

    private void GizmoTranslateEvent(ITransformable transformable, TransformationEventArgs e)
    {
        if (transformable is GizmoTransformableAdapter adapter)
        {
            adapter.Transformable.Position += (Vector3)e.Value;
        }
    }

    private void GizmoRotateEvent(ITransformable transformable, TransformationEventArgs e)
    {
        Gizmo.RotationHelper(transformable, e);
    }

    private void GizmoScaleEvent(ITransformable transformable, TransformationEventArgs e)
    {
        if (transformable is not GizmoTransformableAdapter adapter)
        {
            return;
        }

        var delta = (Vector3)e.Value;
        var scale = adapter.Transformable.Scale;

        if (Gizmo.ActiveMode == GizmoMode.UniformScale)
        {
            scale *= 1 + ((delta.X + delta.Y + delta.Z) / 3);
        }
        else
        {
            scale += delta;
        }

        scale = Vector3.Clamp(scale, Vector3.Zero, scale);
        adapter.Transformable.Scale = scale;
    }
}