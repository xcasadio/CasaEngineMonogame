using CasaEngine.Core.Log;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Transform;
using GizmoTools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components.DebugTools
{
    public class TransformGizmoComponent : DrawableGameComponent
    {
        public Gizmo Gizmo { get; private set; }

        private InputComponent? _inputComponent;
        private CasaEngineGame? _game;
        private readonly Dictionary<ITransformableObject, GizmoTransformableAdapter> _selectionAdapters = [];

        public event EventHandler<List<ITransformableObject>>? DeleteSelectionEvent;
        public event EventHandler<List<ITransformableObject>>? SelectionChanged;
        public event EventHandler<List<ITransformableObject>>? CopyTriggered;

        /// <summary>
        /// When set, <see cref="Update"/> uses this camera instead of
        /// <c>ViewManager.ActiveView.Camera</c>.
        ///
        /// Set by <see cref="CasaEngine.EditorUI.Controls.EngineHost.RegisterEditorView"/> to
        /// bind the gizmo to a specific viewport's camera.
        /// </summary>
        public CameraComponent? ActiveCamera { get; set; }

        /// <summary>
        /// The per-view render-target surface. Used to temporarily override
        /// <c>GraphicsDevice.Viewport</c> before <c>Gizmo.SelectEntities</c> so that
        /// <c>ConvertMouseToRay / Viewport.Unproject</c> uses the correct render-target
        /// dimensions rather than whatever the back buffer happened to be.
        /// Set by EngineHost.RegisterEditorView alongside ActiveCamera.
        /// </summary>
        public RenderTargetSurface? ActiveSurface { get; set; }

        /// <summary>
        /// When <see langword="false"/> this gizmo skips all input processing so that
        /// only the gizmo belonging to the viewport currently under the cursor reacts
        /// to mouse clicks and keyboard shortcuts. Set by EngineHost.Update().
        /// </summary>
        public bool IsActiveViewport { get; set; }

        /// <summary>
        /// World backing this gizmo's selection pool. When null, falls back to
        /// <c>GameManager.CurrentWorld</c> for backward compatibility.
        /// </summary>
        public Framework.World.World? SelectionWorld { get; set; }

        public TransformGizmoComponent(Microsoft.Xna.Framework.Game game) : base(game)
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
                _game.FontSystem.AddFont(File.ReadAllBytes(arialPath));

            Gizmo = new Gizmo(Game.GraphicsDevice);

            Gizmo.TranslateEvent += GizmoTranslateEvent;
            Gizmo.RotateEvent += GizmoRotateEvent;
            Gizmo.ScaleEvent += GizmoScaleEvent;
            Gizmo.SelectionChanged += OnGizmoSelectionChanged;
            Gizmo.DeleteSelectionEvent += OnGizmoDeleteSelectionEvent;
            Gizmo.CopyTriggered += OnGizmoCopyTriggered;

            _inputComponent = Game.GetGameComponent<InputComponent>();

            // Drawing is handled by OverlayViewPipeline.RenderGizmos per-view.
            // Leaving Visible=true would cause a redundant draw in Phase 3 of DrawWithEditor
            // into the EngineHost's dummy back buffer when it is used as an invisible element.
            Visible = false;
        }

        public override void Update(GameTime gameTime)
        {
            var selectionWorld = SelectionWorld ?? _game.GameManager.CurrentWorld;

            if (Gizmo.GetSelectionPool() == null && selectionWorld != null)
            {
                SetSelectionPool(selectionWorld.GetSelectableComponents());
            }

            if (Gizmo.GetSelectionPool() == null)
            {
                return;
            }

            var camera = ActiveCamera ?? _game.GameManager.ViewManager.ActiveView?.Camera;
            if (camera != null)
            {
                Gizmo.UpdateCameraProperties(
                    camera.ViewMatrix,
                    camera.ProjectionMatrix,
                    camera.Position);
            }

            if (!IsActiveViewport) return;

            if (ActiveSurface != null)
            {
                var r = ActiveSurface.ViewportRect;
                Gizmo.ActiveViewport = new Microsoft.Xna.Framework.Graphics.Viewport(r.X, r.Y, r.Width, r.Height);
            }

            var lbState = _inputComponent.MouseManager.LeftButtonJustPressed;

            if (lbState)
            {
                Gizmo.SelectEntities(new Vector2(_inputComponent.MouseManager.Position.X, _inputComponent.MouseManager.Position.Y),
                    _inputComponent.KeyboardManager.IsKeyPressed(Keys.LeftControl) || _inputComponent.KeyboardManager.IsKeyPressed(Keys.RightControl),
                    _inputComponent.KeyboardManager.IsKeyPressed(Keys.LeftAlt) || _inputComponent.KeyboardManager.IsKeyPressed(Keys.RightAlt));
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.D1))
            {
                Gizmo.ActiveMode = GizmoMode.Translate;
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.D2))
            {
                Gizmo.ActiveMode = GizmoMode.Rotate;
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.D3))
            {
                Gizmo.ActiveMode = GizmoMode.NonUniformScale;
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.D4))
            {
                Gizmo.ActiveMode = GizmoMode.UniformScale;
            }

            if (_inputComponent.KeyboardManager.IsKeyPressed(Keys.LeftShift) || _inputComponent.KeyboardManager.IsKeyPressed(Keys.RightShift))
            {
                Gizmo.PrecisionModeEnabled = true;
            }
            else
            {
                Gizmo.PrecisionModeEnabled = false;
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.O))
            {
                Gizmo.ToggleActiveSpace();
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.I))
            {
                Gizmo.SnapEnabled = !Gizmo.SnapEnabled;
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.P))
            {
                Gizmo.NextPivotType();
            }

            if (_inputComponent.KeyboardManager.IsKeyJustPressed(Keys.Escape))
            {
                Gizmo.Clear();
            }

            Gizmo.Update(gameTime, _inputComponent.Keyboard, _inputComponent.MouseState);

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws the gizmo for a specific view using the supplied camera frame.
        /// Called by <see cref="OverlayViewPipeline"/> with the view's render target active.
        /// </summary>
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
}

namespace CasaEngine.Framework.Game.Components.Editor
{
    [System.Obsolete("Use CasaEngine.Framework.Game.Components.DebugTools.TransformGizmoComponent instead.")]
    public sealed class GizmoComponent : CasaEngine.Framework.Game.Components.DebugTools.TransformGizmoComponent
    {
        public GizmoComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
        }
    }
}
