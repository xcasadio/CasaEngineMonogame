﻿#if EDITOR

using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework;
using XNAGizmo;

namespace CasaEngine.Framework.Game.Components.Editor;

public class GizmoComponent : DrawableGameComponent
{
    public Gizmo Gizmo { get; private set; }

    private InputComponent? _inputComponent;
    private CasaEngineGame? _game;

    public GizmoComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        _game = game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Manipulator;
        DrawOrder = (int)ComponentDrawOrder.Manipulator;
    }

    public override void Initialize()
    {
        base.Initialize();

        _game.FontSystem.AddFont(File.ReadAllBytes(@"C:\\Windows\\Fonts\\Arial.ttf"));
        Gizmo = new Gizmo(Game.GraphicsDevice);

        Gizmo.TranslateEvent += GizmoTranslateEvent;
        Gizmo.RotateEvent += GizmoRotateEvent;
        Gizmo.ScaleEvent += GizmoScaleEvent;

        _inputComponent = Game.GetGameComponent<InputComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (Gizmo.GetSelectionPool() == null && _game.GameManager.CurrentWorld != null)
        {
            Gizmo.SetSelectionPool(_game.GameManager.CurrentWorld.GetSelectableComponents());
        }

        if (Gizmo.GetSelectionPool() == null)
        {
            return;
        }

        var camera = _game.GameManager.ActiveCamera;
        if (camera != null)
        {
            Gizmo.UpdateCameraProperties(
                camera.ViewMatrix,
                camera.ProjectionMatrix,
                camera.Position);
        }

        if (_inputComponent.MouseManager.LeftButtonJustPressed)
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
        Gizmo.Draw();
        base.Draw(gameTime);
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