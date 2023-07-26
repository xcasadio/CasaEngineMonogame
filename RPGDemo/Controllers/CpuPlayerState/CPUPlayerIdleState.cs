using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace RPGDemo.Controllers.CpuPlayerState;

public class CpuPlayerIdleState : IState<Controller>
{
    public string Name => "CPUPlayerIdleState";

    public void Enter(Controller controller)
    {
    }

    public void Exit(Controller controller)
    {

    }

    public void Update(Controller controller, float elapsedTime)
    {

    }

    public bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }
}