using CasaEngine.Engine.Input;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.GameFramework;
using CasaEngine.Framework.World;
using CasaEngine.RPGDemo.Controllers.PlayerState;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.RPGDemo.Controllers;

public class HumanPlayerController : Controller
{
    private InputComponent _inputComponent;
    private readonly PlayerIndex _playerIndex;

    public HumanPlayerController(Character character, PlayerIndex index)
        : base(character)
    {
        _playerIndex = index;
        //character.IsPLayer = true;
    }

    public override void InitializePrivate(World world)
    {
        _inputComponent = world.Game.GetGameComponent<InputComponent>();

        StateMachine.GlobalState = new PlayerGlobalStateState();
        AddState((int)PlayerControllerState.Idle, new PlayerIdleState());
        AddState((int)PlayerControllerState.Moving, new PlayerWalkState());
        AddState((int)PlayerControllerState.Attack, new PlayerAttackState());
        AddState((int)PlayerControllerState.Attack2, new PlayerAttack2State());
        AddState((int)PlayerControllerState.Attack3, new PlayerAttack3State());
        AddState((int)PlayerControllerState.Dying, new PlayerDyingState());

        Character.CurrentDirection = Character2dDirection.Right;
        StateMachine.Transition(GetState((int)PlayerControllerState.Idle));
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        //check button/action mapping
        if (Character.Owner.InputEnabled)
        {
            _inputComponent.InputBindingsManager.GetInput("");
        }
    }

    public Character2dDirection GetDirectionFromInput(out Vector2 direction)
    {
        direction = Vector2.Zero;

        if (_inputComponent.IsGamePadConnected(_playerIndex))
        {
            direction = _inputComponent.GamePadState(_playerIndex).ThumbSticks.Left;
            ClampDirection(ref direction);
            direction.Y = -direction.Y;
        }
        else
        {
            if (_inputComponent.IsKeyPressed(Keys.Up))
            {
                direction.Y = -1.0f;
            }
            else if (_inputComponent.IsKeyPressed(Keys.Down))
            {
                direction.Y = 1.0f;
            }

            if (_inputComponent.IsKeyPressed(Keys.Right))
            {
                direction.X = 1.0f;
            }
            else if (_inputComponent.IsKeyPressed(Keys.Left))
            {
                direction.X = -1.0f;
            }
        }

        if (direction.X != 0.0f
            || direction.Y != 0.0f)
        {
            direction.Normalize();
        }

        return Character.GetCharacterDirectionFromVector2(direction * new Vector2(1f, -1f));
    }

    private static void ClampDirection(ref Vector2 direction)
    {
        if (direction.X < -Character.DeadZone)
        {
            direction.X = -1.0f;
        }
        else if (direction.X > Character.DeadZone)
        {
            direction.X = 1.0f;
        }
        else
        {
            direction.X = 0;
        }

        if (direction.Y < -Character.DeadZone)
        {
            direction.Y = -1.0f;
        }
        else if (direction.Y > Character.DeadZone)
        {
            direction.Y = 1.0f;
        }
        else
        {
            direction.Y = 0;
        }
    }

    public bool IsAttackButtonPressed()
    {
        if (_inputComponent.IsGamePadConnected(_playerIndex))
        {
            return _inputComponent.IsButtonJustPressed(_playerIndex, Buttons.A);
        }

        return _inputComponent.IsKeyJustPressed(Keys.Space);
    }
}