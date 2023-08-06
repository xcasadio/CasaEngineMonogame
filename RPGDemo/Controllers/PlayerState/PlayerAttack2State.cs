using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerAttack2State : IState<Controller>
{
    public string Name => "Player Attack2 State";

    public void Enter(Controller controller)
    {
        controller.Character.DoANewAttack();

        Vector2 joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Attack2);
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
        HumanPlayerController c = (HumanPlayerController)controller;

        //if (controller.Character.ComboNumber == 1
        //    && controller.Character.Animation2DPlayer.CurrentAnimation.CurrentFrameIndex >= 2
        //    && c.IsAttackButtonPressed() == true)
        //{
        //    controller.Character.ComboNumber = 2;
        //}
    }

    public bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.AnimationChanged)
        {
            if (controller.Character.ComboNumber == 2)
            {
                controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Attack3));
            }
            else
            {
                controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Idle));
            }

            return true;
        }

        return false;
    }
}