using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace CasaEngine.RPGDemo.Controllers.EnemyState;

public class EnemyGlobalState : IState<Controller>
{
    public string Name => "Enemy Global State";

    public virtual void Enter(Controller controller)
    {

    }

    public virtual void Exit(Controller controller)
    {

    }

    public virtual void Update(Controller controller, float elapsedTime)
    {
        if (controller.Character.HP <= 0)
        {
            controller.GetState((int)EnemyControllerState.Dying);
        }
    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.Hit)
        {
            if (controller.StateMachine.CurrentState != controller.GetState((int)EnemyControllerState.Hit))
            {
                controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Hit));
                return true;
            }
        }

        return false;
    }
}