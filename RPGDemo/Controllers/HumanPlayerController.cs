using CasaEngine.Engine.Input;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RPGDemo.Controllers.PlayerState;
using static RPGDemo.Controllers.Character;

namespace RPGDemo.Controllers;

public class HumanPlayerController : PlayerController
{
    private InputComponent _inputComponent;
    private readonly PlayerIndex _playerIndex;

    //to avoid GC
    private Vector2 _vector2;

    public HumanPlayerController(Character character, PlayerIndex index)
        : base(character)
    {
        _playerIndex = index;
        //character.IsPLayer = true;
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);

        _inputComponent = game.GetGameComponent<InputComponent>();

        AddState((int)PlayerControllerState.Idle, new PlayerIdleState());
        AddState((int)PlayerControllerState.Moving, new PlayerWalkState());
        AddState((int)PlayerControllerState.Attack, new PlayerAttackState());
        AddState((int)PlayerControllerState.Attack2, new PlayerAttack2State());
        AddState((int)PlayerControllerState.Attack3, new PlayerAttack3State());

        Character.CurrentDirection = Character2dDirection.Right;
        Character.SetAnimation(AnimationIndices.Idle);
        StateMachine.Transition(GetState((int)PlayerControllerState.Idle));
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);
    }

    public Character2dDirection GetDirectionFromInput(out Vector2 direction)
    {
        direction = Vector2.Zero;

        if (_inputComponent.IsGamePadConnected(_playerIndex) == true)
        {
            direction = _inputComponent.GamePadState(_playerIndex).ThumbSticks.Left;
        }
        else
        {
            if (_inputComponent.IsKeyPressed(Keys.Up) == true)
            {
                direction.Y = 1.0f;
            }
            else if (_inputComponent.IsKeyPressed(Keys.Down) == true)
            {
                direction.Y = -1.0f;
            }

            if (_inputComponent.IsKeyPressed(Keys.Right) == true)
            {
                direction.X = 1.0f;
            }
            else if (_inputComponent.IsKeyPressed(Keys.Left) == true)
            {
                direction.X = -1.0f;
            }
        }

        if (direction.X < -0.2f)
        {
            direction.X = -1.0f;
        }
        else if (direction.X > 0.2f)
        {
            direction.X = 1.0f;
        }
        else
        {
            direction.X = 0;
        }

        if (direction.Y < -0.2f)
        {
            direction.Y = -1.0f;
        }
        else if (direction.Y > 0.2f)
        {
            direction.Y = 1.0f;
        }
        else
        {
            direction.Y = 0;
        }

        if (direction.X != 0.0f
            || direction.Y != 0.0f)
        {
            direction.Normalize();
        }

        _vector2 = direction;
        _vector2.Y = -_vector2.Y; //inverse to be on screen coordinate

        return Controllers.Character.GetCharacterDirectionFromVector2(_vector2);
    }

    public bool IsAttackButtonPressed()
    {
        if (_inputComponent.IsGamePadConnected(_playerIndex) == true)
        {
            return _inputComponent.IsButtonJustPressed(_playerIndex, Buttons.A);
        }

        return _inputComponent.IsKeyJustPressed(Keys.Space);
    }
}