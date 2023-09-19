using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace CasaEngine.RPGDemo.Controllers.EnemyState;

public class EnemyDyingState : IState<Controller>
{
    public string Name => "Enemy Dying State";

    public void Enter(Controller controller)
    {
        controller.Character.Dying();
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