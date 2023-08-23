using CasaEngine.Core.Helpers;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.EnemyState;

public class EnemyIdleState : IState<Controller>
{
    private float _elapsedTime;
    private float _timeMaxBeforeHunt;

    public string Name => "Enemy Idle State";

    public virtual void Enter(Controller controller)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Idle);
        _elapsedTime = 0;
        var random = RandomExtension.Create();
        _timeMaxBeforeHunt = random.NextFloat(5.0f, 10.0f);
    }

    public virtual void Exit(Controller controller)
    {

    }

    public virtual void Update(Controller controller, float elapsedTime)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);

        _elapsedTime += elapsedTime;

        if (_elapsedTime >= _timeMaxBeforeHunt)
        {
            controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Hunt));
        }
        else
        {
            var ec = (EnemyController)controller;

            if (ec.PlayerHunted != null)
            {
                var dir = (ec.PlayerHunted.Position - controller.Character.Position).ToVector2(); //inverse
                dir.Normalize();
                controller.Character.CurrentDirection = Character.GetCharacterDirectionFromVector2(dir);
                controller.Character.SetAnimation(Character.AnimationIndices.Idle);
            }
        }
    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }
}