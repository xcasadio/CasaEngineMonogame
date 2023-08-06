using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerIdleState : IState<Controller>
{
    public string Name => "Player Idle State";

    public void Enter(Controller controller)
    {
        Vector2 joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Idle);
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
        Character2dDirection dir;
        Vector2 joyDir;
        HumanPlayerController c = (HumanPlayerController)controller;
        //PlayerIndex playerIndex = c.PlayerIndex;

        if (c.IsAttackButtonPressed())
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Attack));
            return;
        }

        dir = c.GetDirectionFromInput(out joyDir);

        if (dir != 0)
        {
            controller.Character.CurrentDirection = dir;
        }

        if (joyDir.X != 0.0f
            || joyDir.Y != 0.0f)
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Moving));
            return;
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