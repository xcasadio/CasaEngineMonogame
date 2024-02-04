using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;

namespace CasaEngine.EditorUI.Controls;

public abstract class GameEditor2d : GameEditor
{
    private float _scale = 1.0f;
    protected AActor _entity;
    private InputComponent? _inputComponent;
    private Point _lastMousePosition;
    private Camera3dIn2dAxisComponent _cameraComponent;
    protected AActor CameraEntity { get; private set; }

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

    protected abstract void CreateEntityComponents(AActor entity);


    protected override void LoadContent()
    {
        Game.IsRunningInGameEditorMode = false;
        Game.GameManager.CreateCameraComponentCallback = CreateCameraComponent;

        _inputComponent = Game.GetGameComponent<InputComponent>();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());

        Game.GameManager.SetWorldToLoad(new World());

        _entity = new AActor { Name = "actor GameEditor2d" }; ;
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

        Game.GameManager.CurrentWorld.AddEntityWithEditor(_entity);
    }

    private CameraComponent CreateCameraComponent(AActor cameraEntity)
    {
        var screenXBy2 = Game.ScreenSizeWidth / 2f;
        var screenYBy2 = Game.ScreenSizeHeight / 2f;

        CameraEntity = cameraEntity;
        _cameraComponent = new Camera3dIn2dAxisComponent();
        _cameraComponent.Target = new Vector3(screenXBy2, screenYBy2, 0f);

        return _cameraComponent;
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (_inputComponent.MouseRightButtonJustPressed)
        {
            _lastMousePosition = _inputComponent.MousePos;
        }
        else if (_inputComponent.MouseRightButtonPressed)
        {
            var delta = _lastMousePosition - _inputComponent.MousePos;
            _cameraComponent.Target += new Vector3(delta.X, -delta.Y, 0f);
            _lastMousePosition = _inputComponent.MousePos;
        }

        if (_inputComponent.MouseWheelDelta > 0 && _scale < 8.0f)
        {
            _scale *= 2.0f;
        }
        else if (_inputComponent.MouseWheelDelta > 0 && _scale > 1.0f)
        {
            _scale /= 2.0f;
        }
    }
}