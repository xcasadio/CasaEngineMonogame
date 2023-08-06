using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerWalkState : IState<Controller>
{
    public string Name => "Player Walk State";

    public void Enter(Controller controller)
    {
        Vector2 joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Walk);
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
        HumanPlayerController c = (HumanPlayerController)controller;
        //PlayerIndex playerIndex = c.PlayerIndex;

        if (c.IsAttackButtonPressed())
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Attack));
            return;
        }

        var dir = c.GetDirectionFromInput(out var joyDir);

        if (joyDir.X == 0.0f
            && joyDir.Y == 0.0f)
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Idle));
            return;
        }

        if (dir != 0)
        {
            controller.Character.CurrentDirection = dir;
        }

        controller.Character.SetAnimation(Character.AnimationIndices.Walk);

        joyDir.Y = -joyDir.Y;

        controller.Character.Move(ref joyDir);
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