using CasaEngine.Core.Helpers;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;

namespace RPGDemo.Controllers.EnemyState;

public class EnemyMoveToState : IState<Controller>
{
    public string Name => "Enemy Move To State";

    public virtual void Enter(Controller controller)
    {

    }

    public virtual void Exit(Controller controller)
    {

    }

    public virtual void Update(Controller controller, float elapsedTime)
    {
        //move to player
        if (controller is EnemyController ec)
        {
            var dir = ec.TargetPosition - ec.Character.Position.ToVector2();

            if (dir.LengthSquared() <= 10 * 10)
            {
                ec.StateMachine.RevertStateChange();
            }
            else
            {
                dir.Normalize();

                ec.Character.Move(ref dir);
                ec.Character.CurrentDirection = Character.GetCharacterDirectionFromVector2(dir);
                ec.Character.SetAnimation(Character.AnimationIndices.Walk);
            }
        }
    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }
}