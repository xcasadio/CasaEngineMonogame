using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XNAGizmo;

namespace CasaEngine.Framework.Game.Components;

public class GizmoComponent : DrawableGameComponent
{
    private Gizmo _gizmo;

    private KeyboardState _previousKeys;
    private MouseState _previousMouse;
    private MouseState _currentMouse;
    private KeyboardState _currentKeys;

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

        _gizmo.UpdateCameraProperties(
            GameInfo.Instance.ActiveCamera.ViewMatrix,
            GameInfo.Instance.ActiveCamera.ProjectionMatrix,
            GameInfo.Instance.ActiveCamera.Position);

        _currentMouse = Mouse.GetState();
        _currentKeys = Keyboard.GetState();

        if (_currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
        {
            _gizmo.SelectEntities(new Vector2(_currentMouse.X, _currentMouse.Y),
                _currentKeys.IsKeyDown(Keys.LeftControl) || _currentKeys.IsKeyDown(Keys.RightControl),
                _currentKeys.IsKeyDown(Keys.LeftAlt) || _currentKeys.IsKeyDown(Keys.RightAlt));
        }

        if (IsNewButtonPress(Keys.D1))
        {
            _gizmo.ActiveMode = GizmoMode.Translate;
        }

        if (IsNewButtonPress(Keys.D2))
        {
            _gizmo.ActiveMode = GizmoMode.Rotate;
        }

        if (IsNewButtonPress(Keys.D3))
        {
            _gizmo.ActiveMode = GizmoMode.NonUniformScale;
        }

        if (IsNewButtonPress(Keys.D4))
        {
            _gizmo.ActiveMode = GizmoMode.UniformScale;
        }

        if (_currentKeys.IsKeyDown(Keys.LeftShift) || _currentKeys.IsKeyDown(Keys.RightShift))
        {
            _gizmo.PrecisionModeEnabled = true;
        }
        else
        {
            _gizmo.PrecisionModeEnabled = false;
        }

        if (IsNewButtonPress(Keys.O))
        {
            _gizmo.ToggleActiveSpace();
        }

        if (IsNewButtonPress(Keys.I))
        {
            _gizmo.SnapEnabled = !_gizmo.SnapEnabled;
        }

        if (IsNewButtonPress(Keys.P))
        {
            _gizmo.NextPivotType();
        }

        if (IsNewButtonPress(Keys.Escape))
        {
            _gizmo.Clear();
        }

        _gizmo.Update(gameTime);

        _previousKeys = _currentKeys;
        _previousMouse = _currentMouse;

        base.Update(gameTime);
    }

    private bool IsNewButtonPress(Keys key)
    {
        return _currentKeys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);
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