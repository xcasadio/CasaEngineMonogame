using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace CasaEngine.RPGDemo.Controllers.PlayerState;

public abstract class PlayerBaseState : IState<Controller>
{
    public abstract string Name
    {
        get;
    }

    public virtual void Enter(Controller controller)
    {

    }

    public virtual void Exit(Controller controller)
    {

    }

    public virtual void Update(Controller controller, float elapsedTime)
    {

    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }
}