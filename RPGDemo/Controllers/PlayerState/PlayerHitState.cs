using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace RPGDemo.Controllers.PlayerState;

public class PlayerHitState : IState<Controller>
{
    public string Name => "Player Hit State";

    public void Enter(Controller controller)
    {
        controller.Character.SetAnimation((int)Character.AnimationIndices.Hit);
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {

    }

    public bool HandleMessage(Controller controller, Message message)
    {
        if (message.Type == (int)MessageType.AnimationChanged)
        {
            controller.StateMachine.Transition(controller.GetState((int)PlayerControllerState.Idle));
        }

        return false;
    }
}