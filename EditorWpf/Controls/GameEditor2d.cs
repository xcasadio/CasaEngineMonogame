using CasaEngine.Engine.Input;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using CasaEngine.Framework.World;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls;

public abstract class GameEditor2d : GameEditor
{
    private float _scale = 1.0f;
    private Entity _entity;
    private InputComponent? _inputComponent;
    private Point _lastMousePosition;

    public float Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            _entity.Coordinates.LocalScale = new Vector3(_scale);
        }
    }

    protected abstract void CreateEntityComponents(Entity entity);

    protected override void LoadContent()
    {
        base.LoadContent();
        _inputComponent = Game.GetGameComponent<InputComponent>();
        Game.Components.Remove(Game.GetGameComponent<GridComponent>());
        Game.GameManager.CurrentWorld = new World();

        _entity = new Entity();
        _entity.Coordinates.LocalPosition = new Vector3(300f, 300f, 0.0f);

        CreateEntityComponents(_entity);

        _entity.Initialize(Game);
        Game.GameManager.CurrentWorld.AddEntityImmediately(_entity);
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
            _entity.Coordinates.LocalPosition += new Vector3(-delta.X, -delta.Y, _entity.Coordinates.LocalPosition.Z);
            _lastMousePosition = _inputComponent.MousePos;
        }
    }
}