using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;

namespace CasaEngine.EditorUI.Controls;

public abstract class GameEditor2d : GameEditor
{
    private float _scale = 1.0f;
    protected Entity _entity;
    private InputComponent? _inputComponent;
    private Point _lastMousePosition;
    protected Entity CameraEntity { get; private set; }
    protected Camera3dIn2dAxisComponent CameraComponent { get; private set; }

    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            _entity.RootComponent.Coordinates.Scale = new Vector3(_scale);
        }
    }

    protected GameEditor2d(bool useGui = false) : base(useGui)
    {
    }

    protected abstract void CreateEntityComponents(Entity entity);

    protected override void LoadContent()
    {
        Game.IsRunningInGameEditorMode = false;
        Game.GameManager.CreateCameraComponentCallback = CreateCameraComponent;

        _inputComponent = Game.GetGameComponent<InputComponent>();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());

        Game.GameManager.SetWorldToLoad(new World());

        _entity = new Entity { Name = "actor GameEditor2d" };
        CreateEntityComponents(_entity);
        if (_entity.RootComponent != null)
        {
            var screenXBy2 = Game.ScreenSizeWidth / 2f;
            var screenYBy2 = Game.ScreenSizeHeight / 2f;
            _entity.RootComponent.Coordinates.Position = new Vector3(screenXBy2, screenYBy2, 0.0f);
        }

        _entity.Initialize();
        Game.GameManager.CurrentWorld.LoadContent(Game);
        _entity.InitializeWithWorld(Game.GameManager.CurrentWorld);

        base.LoadContent();

        Game.GameManager.CurrentWorld.AddEntity(_entity);
        //Game.GameManager.CurrentWorld.AddEntityWithEditor(_entity);
    }

    private CameraComponent CreateCameraComponent(Entity cameraEntity)
    {
        var screenXBy2 = Game.ScreenSizeWidth / 2f;
        var screenYBy2 = Game.ScreenSizeHeight / 2f;

        CameraEntity = cameraEntity;
        CameraComponent = new Camera3dIn2dAxisComponent();
        CameraComponent.Target = new Vector3(screenXBy2, screenYBy2, 0f);

        return CameraComponent;
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (_inputComponent.MouseManager.RightButtonJustPressed)
        {
            _lastMousePosition = _inputComponent.MouseManager.Position;
        }
        else if (_inputComponent.MouseManager.RightButtonPressed)
        {
            var delta = _lastMousePosition - _inputComponent.MouseManager.Position;
            CameraComponent.Target += new Vector3(delta.X, -delta.Y, 0f);
            _lastMousePosition = _inputComponent.MouseManager.Position;
        }

        if (_inputComponent.MouseManager.WheelDelta > 0 && _scale < 8.0f)
        {
            _scale *= 2.0f;
        }
        else if (_inputComponent.MouseManager.WheelDelta > 0 && _scale > 1.0f)
        {
            _scale /= 2.0f;
        }
    }
}