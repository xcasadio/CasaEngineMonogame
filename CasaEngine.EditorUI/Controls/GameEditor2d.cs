using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
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
        base.LoadContent();

        CameraEntity = new AActor();
        var camera = new Camera3dIn2dAxisComponent();
        CameraEntity.RootComponent = camera;
        var screenXBy2 = Game.ScreenSizeWidth / 2f;
        var screenYBy2 = Game.ScreenSizeHeight / 2f;
        camera.Target = new Vector3(screenXBy2, screenYBy2, 0.0f);
        CameraEntity.Initialize();
        //CameraEntity.InitializeWithWorld(Game.GameManager.CurrentWorld);
        Game.GameManager.ActiveCamera = camera;

        _inputComponent = Game.GetGameComponent<InputComponent>();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());
        Game.GameManager.SetWorldToLoad(new World());
        //Game.GameManager.CurrentWorld.LoadContent(Game);

        _entity = new AActor();
        CreateEntityComponents(_entity);
        if (_entity.RootComponent != null)
        {
            _entity.RootComponent.Coordinates.Position = new Vector3(screenXBy2, screenYBy2, 0.0f);
        }

        _entity.Initialize();
        //_entity.InitializeWithWorld(Game.GameManager.CurrentWorld);
        Game.GameManager.CurrentWorld.AddEntity(_entity);
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
            _entity.RootComponent.Coordinates.Position += new Vector3(-delta.X, delta.Y, _entity.RootComponent.Coordinates.Position.Z);
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