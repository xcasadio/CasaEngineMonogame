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

        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Attack2);
        controller.Character.AttachWeapon();
    }

    public void Exit(Controller controller)
    {
        controller.Character.UnAttachWeapon();
    }

    public void Update(Controller controller, float elapsedTime)
    {
        var c = (HumanPlayerController)controller;

        if (controller.Character.ComboNumber == 1
            //&& controller.Character.AnimatedSpriteComponent.CurrentAnimation.CurrentFrameIndex >= 2
            && c.IsAttackButtonPressed() == true)
        {
            controller.Character.ComboNumber = 2;
        }
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