using CasaEngine.Framework.Application;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Scene.World;
using CasaEngine.RPGDemo.Controllers.PlayerState;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.RPGDemo.Controllers;

public class HumanPlayerController : Controller
{
    private PlayerInput _playerInput;
    private readonly PlayerIndex _playerIndex;

    public HumanPlayerController(Character character, PlayerIndex index)
        : base(character)
    {
        _playerIndex = index;
        //character.IsPLayer = true;
    }

    public override void InitializePrivate(World world)
    {
        _playerInput = world.GetPlayerController(Character.Owner)?.Input;
        if (_playerInput == null)
        {
            var playerController = new PlayerController
            {
                Pawn = Character.Owner,
                Player = new LocalPlayer { ControllerId = _playerIndex },
            };
            _playerInput = new PlayerInput(playerController, world.Game.GetGameComponent<InputComponent>());
        }

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
        if (_playerInput != null && _playerInput.IsInputEnabled)
        {
            //_playerInput.GetButtonState("...");
        }
    }

    public Character2dDirection GetDirectionFromInput(out Vector2 direction)
    {
        direction = Vector2.Zero;

        if (_playerInput.IsGamePadConnected)
        {
            direction = _playerInput.ThumbStickLeft;
            ClampDirection(ref direction);
            direction.Y = -direction.Y;
        }
        else
        {
            if (_playerInput.IsKeyPressed(Keys.Up))
            {
                direction.Y = -1.0f;
            }
            else if (_playerInput.IsKeyPressed(Keys.Down))
            {
                direction.Y = 1.0f;
            }

            if (_playerInput.IsKeyPressed(Keys.Right))
            {
                direction.X = 1.0f;
            }
            else if (_playerInput.IsKeyPressed(Keys.Left))
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
        return _playerInput.IsGamePadConnected
            ? _playerInput.IsButtonJustPressed(Buttons.A)
            : _playerInput.IsKeyJustPressed(Keys.Space);
    }
}