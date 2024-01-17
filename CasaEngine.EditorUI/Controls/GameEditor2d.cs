using System.Windows;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.Game.Components.Editor;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;

namespace CasaEngine.Editor.Controls;

public abstract class GameEditor2d : GameEditor
{
    private float _scale = 1.0f;
    protected Entity _entity;
    private InputComponent? _inputComponent;
    private Point _lastMousePosition;
    protected Entity CameraEntity { get; private set; }

    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            _entity.Coordinates.LocalScale = new Vector3(_scale);
        }
    }

    protected GameEditor2d(bool useGui = false) : base(useGui)
    {
    }

    protected abstract void CreateEntityComponents(Entity entity);


    protected override void LoadContent()
    {
        base.LoadContent();

        CameraEntity = new Entity();
        var camera = new Camera3dIn2dAxisComponent();
        CameraEntity.ComponentManager.Add(camera);
        var screenXBy2 = Game.ScreenSizeWidth / 2f;
        var screenYBy2 = Game.ScreenSizeHeight / 2f;
        camera.Target = new Vector3(screenXBy2, screenYBy2, 0.0f);
        CameraEntity.Initialize(Game);
        Game.GameManager.ActiveCamera = camera;

        _inputComponent = Game.GetGameComponent<InputComponent>();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());
        Game.GameManager.CurrentWorld = new World();
        Game.GameManager.CurrentWorld.Initialize(Game);

        _entity = new Entity();
        _entity.Coordinates.LocalPosition = new Vector3(screenXBy2, screenYBy2, 0.0f);

        CreateEntityComponents(_entity);

        _entity.Initialize(Game);
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
            _entity.Coordinates.LocalPosition += new Vector3(-delta.X, delta.Y, _entity.Coordinates.LocalPosition.Z);
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