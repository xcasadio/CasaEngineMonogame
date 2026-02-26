#if EDITOR

using CasaEngine.Core.Log;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using XNAGizmo;

namespace CasaEngine.Framework.Game.Components.Editor;

public class GizmoComponent : DrawableGameComponent
{
    public Gizmo Gizmo { get; private set; }

    private InputComponent? _inputComponent;
    private CasaEngineGame? _game;

    /// <summary>
    /// When set, <see cref="Update"/> uses this camera instead of
    /// <c>ViewManager.ActiveView.Camera</c>.
    ///
    /// Set by <see cref="CasaEngine.EditorUI.Controls.EngineHost.RegisterEditorView"/> to
    /// bind the gizmo to a specific viewport's camera.
    /// </summary>
    public CameraComponent? ActiveCamera { get; set; }

    /// <summary>
    /// When <see langword="false"/> this gizmo skips all input processing so that
    /// only the gizmo belonging to the viewport currently under the cursor reacts
    /// to mouse clicks and keyboard shortcuts. Set by EngineHost.Update().
    /// </summary>
    public bool IsActiveViewport { get; set; }

    public GizmoComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Manipulator;
        DrawOrder = (int)ComponentDrawOrder.Manipulator;
    }

    public override void Initialize()
    {
        if (Gizmo != null) return;  // already fully initialized — prevent double-init

        base.Initialize();

        var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var arialPath = Path.Combine(fontsDir, "arial.ttf");
        if (File.Exists(arialPath))
            _game.FontSystem.AddFont(File.ReadAllBytes(arialPath));

        Gizmo = new Gizmo(Game.GraphicsDevice);

        Gizmo.TranslateEvent += GizmoTranslateEvent;
        Gizmo.RotateEvent += GizmoRotateEvent;
        Gizmo.ScaleEvent += GizmoScaleEvent;

        _inputComponent = Game.GetGameComponent<InputComponent>();

        // Drawing is handled by EditorViewPipeline.RenderGizmos per-view.
        // Leaving Visible=true would cause a redundant draw in Phase 3 of DrawWithEditor
        // into the EngineHost's dummy back buffer when it is used as an invisible element.
        Visible = false;
    }

    public override void Update(GameTime gameTime)
    {
        if (Gizmo.GetSelectionPool() == null && _game.GameManager.CurrentWorld != null)
        {
            Gizmo.SetSelectionPool(_game.GameManager.CurrentWorld.GetSelectableComponents());
        }

        if (Gizmo.GetSelectionPool() == null)
        {
            Logs.WriteDebug("[InputDiag] GizmoComponent: early return — SelectionPool is null");
            return;
        }

        var camera = ActiveCamera ?? _game.GameManager.ViewManager.ActiveView?.Camera;
        if (camera == null)
            Logs.WriteDebug("[InputDiag] GizmoComponent: camera is null, gizmo camera props not updated");
        else
        {
            Gizmo.UpdateCameraProperties(
                camera.ViewMatrix,
                camera.ProjectionMatrix,
                camera.Position);
        }

        // Only process input for the viewport currently under the cursor.
        if (!IsActiveViewport) return;

        var lbState = _inputComponent.MouseManager.LeftButtonJustPressed;

        if (lbState)
        {
            Logs.WriteDebug($"[InputDiag] GizmoComponent: LeftButtonJustPressed pos=({_inputComponent.MouseManager.Position.X},{_inputComponent.MouseManager.Position.Y})");
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
        // Visible = false — this override is never called from DrawWithEditor.
        // Drawing is done by DrawForView() called from EditorViewPipeline.
        base.Draw(gameTime);
    }

    /// <summary>
    /// Draws the gizmo for a specific view using the supplied camera frame.
    /// Called by <see cref="EditorViewPipeline"/> with the view's render target active.
    /// </summary>
    public void DrawForView(in RenderFrame frame)
    {
        // Re-apply camera matrices from the RenderFrame to ensure the gizmo uses
        // this viewport's projection (Update() may have used a different camera if
        // ActiveCamera was not set yet).
        Gizmo.UpdateCameraProperties(frame.View, frame.Projection, frame.CameraPosition);
        Gizmo.Draw();
    }

    private void GizmoTranslateEvent(ITransformable transformable, TransformationEventArgs e)
    {
        transformable.Position += (Vector3)e.Value;
    }

    private void GizmoRotateEvent(ITransformable transformable, TransformationEventArgs e)
    {
        Gizmo.RotationHelper(transformable, e);
    }

    private void GizmoScaleEvent(ITransformable transformable, TransformationEventArgs e)
    {
        var delta = (Vector3)e.Value;
        var scale = transformable.Scale;

        if (Gizmo.ActiveMode == GizmoMode.UniformScale)
        {
            scale *= 1 + ((delta.X + delta.Y + delta.Z) / 3);
        }
        else
        {
            scale += delta;
        }
        scale = Vector3.Clamp(scale, Vector3.Zero, scale);
        transformable.Scale = scale;
    }
}

#endif