using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace CasaEngine.RPGDemo.Controllers.PlayerState;

public class PlayerGlobalStateState : IState<Controller>
{
    public string Name
    {
        get;
    }

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