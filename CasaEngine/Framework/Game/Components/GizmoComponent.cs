using CasaEngine.Engine.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XNAGizmo;

namespace CasaEngine.Framework.Game.Components;

public class GizmoComponent : DrawableGameComponent
{
    private Gizmo _gizmo;

    private InputComponent? _inputComponent;

    public GizmoComponent(Microsoft.Xna.Framework.Game game) : base(game)
    {
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.Manipulator;
        DrawOrder = (int)ComponentDrawOrder.Manipulator;
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        var spriteBatch = new SpriteBatch(GraphicsDevice);

        var font = Game.Content.Load<SpriteFont>("GizmoFont");
        _gizmo = new Gizmo(Game.GraphicsDevice, spriteBatch, font);

        _gizmo.TranslateEvent += GizmoTranslateEvent;
        _gizmo.RotateEvent += GizmoRotateEvent;
        _gizmo.ScaleEvent += GizmoScaleEvent;

        _inputComponent = Game.GetGameComponent<InputComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_gizmo.GetSelectionPool() == null && GameInfo.Instance.CurrentWorld != null)
        {
            _gizmo.SetSelectionPool(GameInfo.Instance.CurrentWorld.Entities);
        }
        else if (_gizmo.GetSelectionPool() == null)
        {
            return;
        }

        if (GameInfo.Instance.ActiveCamera != null)
        {
            _gizmo.UpdateCameraProperties(
                GameInfo.Instance.ActiveCamera.ViewMatrix,
                GameInfo.Instance.ActiveCamera.ProjectionMatrix,
                GameInfo.Instance.ActiveCamera.Position);
        }

        if (_inputComponent.MouseLeftButtonJustPressed)
        {
            _gizmo.SelectEntities(new Vector2(_inputComponent.MousePos.X, _inputComponent.MousePos.Y),
                _inputComponent.IsKeyPressed(Keys.LeftControl) || _inputComponent.IsKeyPressed(Keys.RightControl),
                _inputComponent.IsKeyPressed(Keys.LeftAlt) || _inputComponent.IsKeyPressed(Keys.RightAlt));
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D1))
        {
            _gizmo.ActiveMode = GizmoMode.Translate;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D2))
        {
            _gizmo.ActiveMode = GizmoMode.Rotate;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D3))
        {
            _gizmo.ActiveMode = GizmoMode.NonUniformScale;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.D4))
        {
            _gizmo.ActiveMode = GizmoMode.UniformScale;
        }

        if (_inputComponent.IsKeyPressed(Keys.LeftControl) || _inputComponent.IsKeyPressed(Keys.RightControl))
        {
            _gizmo.PrecisionModeEnabled = true;
        }
        else
        {
            _gizmo.PrecisionModeEnabled = false;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.O))
        {
            _gizmo.ToggleActiveSpace();
        }

        if (_inputComponent.IsKeyJustPressed(Keys.I))
        {
            _gizmo.SnapEnabled = !_gizmo.SnapEnabled;
        }

        if (_inputComponent.IsKeyJustPressed(Keys.P))
        {
            _gizmo.NextPivotType();
        }

        if (_inputComponent.IsKeyJustPressed(Keys.Escape))
        {
            _gizmo.Clear();
        }

        _gizmo.Update(gameTime, _inputComponent.Keyboard, _inputComponent.MouseState);

        base.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        _gizmo.Draw();
        base.Draw(gameTime);
    }

    private void GizmoTranslateEvent(ITransformable transformable, TransformationEventArgs e)
    {
        transformable.Position += (Vector3)e.Value;
    }

    private void GizmoRotateEvent(ITransformable transformable, TransformationEventArgs e)
    {
        _gizmo.RotationHelper(transformable, e);
    }

    private void GizmoScaleEvent(ITransformable transformable, TransformationEventArgs e)
    {
        var delta = (Vector3)e.Value;
        if (_gizmo.ActiveMode == GizmoMode.UniformScale)
        {
            transformable.Scale *= 1 + ((delta.X + delta.Y + delta.Z) / 3);
        }
        else
        {
            transformable.Scale += delta;
        }

        transformable.Scale = Vector3.Clamp(transformable.Scale, Vector3.Zero, transformable.Scale);
    }
}