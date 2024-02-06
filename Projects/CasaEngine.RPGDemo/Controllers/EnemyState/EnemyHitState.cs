using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace CasaEngine.RPGDemo.Controllers.EnemyState;

public class EnemyHitState : IState<Controller>
{
    public string Name => "Enemy Hit State";

    private float _hitTime;

    public void Enter(Controller controller)
    {
        controller.Character.SetAnimation(Character.AnimationIndices.Hit);
        _hitTime = 0.3f;
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {
        _hitTime -= elapsedTime;

        if (_hitTime <= 0)
        {
            controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Idle));
        }
    }

    public bool HandleMessage(Controller controller, Message message)
    {
        //if (message.Type == (int)MessageType.AnimationChanged)
        //{
        //    controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Idle));
        //}

        return false;
    }
}