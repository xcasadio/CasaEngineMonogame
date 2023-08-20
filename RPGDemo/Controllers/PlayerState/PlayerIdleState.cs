using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerIdleState : IState<Controller>
{
    public string Name => "Player Idle State";

    public void Enter(Controller controller)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Idle);
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
        var c = (HumanPlayerController)controller;
        //PlayerIndex playerIndex = c.PlayerIndex;

        if (c.IsAttackButtonPressed() && controller.Character.CanAttack)
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Attack));
            return;
        }

        var dir = c.GetDirectionFromInput(out var joyDir);

        if (dir != 0)
        {
            controller.Character.CurrentDirection = dir;
        }

        if (joyDir.X != 0.0f
            || joyDir.Y != 0.0f)
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Moving));
        }
        else // used to immobilized the character
        {
            joyDir = Vector2.Zero;
            controller.Character.Move(ref joyDir);
        }
    }

    public bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.Hit)
        {
            //controller.Character.Hit();
            return true;
        }

        //if (message.Type == MessageType.IHitSomeone)
        //{
        //    //controller.Character.IHitSomeone();
        //    return true;
        //}

        return false;
    }
}