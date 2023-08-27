using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerAttackState : IState<Controller>
{
    public string Name => "Player Attack State";

    public void Enter(Controller controller)
    {
        controller.Character.DoANewAttack();
        controller.Character.ComboNumber = 0;
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Attack);
        controller.Character.AttachWeapon();
    }

    public void Exit(Controller controller)
    {
        controller.Character.UnAttachWeapon();
    }

    public void Update(Controller controller, float elapsedTime)
    {
        var c = (HumanPlayerController)controller;

        if (controller.Character.ComboNumber == 0
            //&& controller.Character.AnimatedSpriteComponent.CurrentAnimation.CurrentFrameIndex >= 2
            && c.IsAttackButtonPressed() == true)
        {
            controller.Character.ComboNumber = 1;
        }
    }

    public bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.AnimationChanged && message.SenderID == controller.Character.Owner.Id)
        {
            if (controller.Character.ComboNumber == 1)
            {
                controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Attack2));
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