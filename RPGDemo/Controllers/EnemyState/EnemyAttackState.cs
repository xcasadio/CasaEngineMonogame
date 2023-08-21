using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.EnemyState;

public class EnemyAttackState : IState<Controller>
{
    public string Name => "Enemy Attack";

    public virtual void Enter(Controller controller)
    {
        controller.Character.DoANewAttack();
        controller.Character.ComboNumber = 0;
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Attack);
        controller.Character.AttachWeapon();
    }

    public virtual void Exit(Controller controller)
    {
        controller.Character.UnAttachWeapon();
    }

    public virtual void Update(Controller controller, float elapsedTime)
    {

    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.AnimationChanged)
        {
            controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Idle));
            return true;
        }

        return false;
    }
}