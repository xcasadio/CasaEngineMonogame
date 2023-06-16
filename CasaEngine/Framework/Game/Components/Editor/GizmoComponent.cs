using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XNAGizmo;

namespace CasaEngine.Framework.Game.Components.Editor;

#if EDITOR

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

    protected override void LoadContent()
    {
        base.LoadContent();
        var spriteBatch = new SpriteBatch(GraphicsDevice);

        var font = Game.Content.Load<SpriteFont>("GizmoFont");
        Gizmo = new Gizmo(Game.GraphicsDevice, spriteBatch, font);

        Gizmo.TranslateEvent += GizmoTranslateEvent;
        Gizmo.RotateEvent += GizmoRotateEvent;
        Gizmo.ScaleEvent += GizmoScaleEvent;

        _inputComponent = Game.GetGameComponent<InputComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (Gizmo.GetSelectionPool() == null && _game.GameManager.CurrentWorld != null)
        {
            Gizmo.SetSelectionPool(_game.GameManager.CurrentWorld.Entities);
        }
        else if (Gizmo.GetSelectionPool() == null)
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

        if (_inputComponent.MouseLeftButtonJustPressed)
        {
            Gizmo.SelectEntities(new Vector2(_inputComponent.MousePos.X, _inputComponent.MousePos.Y),
                _inputComponent.IsKeyPressed(Keys.LeftControl) || _inputComponent.IsKeyPressed(Keys.RightControl),
                _inputComponent.IsKeyPressed(Keys.LeftAlt) || _inputComponent.IsKeyPressed(Keys.RightAlt));
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D1))
        {
            Gizmo.ActiveMode = GizmoMode.Translate;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D2))
        {
            Gizmo.ActiveMode = GizmoMode.Rotate;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D3))
        {
            Gizmo.ActiveMode = GizmoMode.NonUniformScale;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D4))
        {
            Gizmo.ActiveMode = GizmoMode.UniformScale;
        }

        if (_inputComponent.IsKeyPressed(Keys.LeftControl) || _inputComponent.IsKeyPressed(Keys.RightControl))
        {
            Gizmo.PrecisionModeEnabled = true;
        }
        else
        {
            Gizmo.PrecisionModeEnabled = false;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.O))
        {
            Gizmo.ToggleActiveSpace();
        }

        if (_inputComponent.IsKeyJustPressed(Keys.I))
        {
            Gizmo.SnapEnabled = !Gizmo.SnapEnabled;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.P))
        {
            Gizmo.NextPivotType();
        }

        if (_inputComponent.IsKeyJustPressed(Keys.Escape))
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