using System;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Controllers.EnemyState;

public class EnemyIdleState : IState<Controller>
{
    private float _timeMaxBeforeHunt;
    private float _idleTime;
    private readonly Random _random = RandomExtension.Create();

    public string Name => "Enemy Idle State";

    public virtual void Enter(Controller controller)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);
        controller.Character.SetAnimation(Character.AnimationIndices.Stand);
        _timeMaxBeforeHunt = _random.NextFloat(5.0f, 10.0f);
        ComputeIdleTime();
    }

    public virtual void Exit(Controller controller)
    {

    }

    public virtual void Update(Controller controller, float elapsedTime)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);

        _timeMaxBeforeHunt -= elapsedTime;
        _idleTime -= elapsedTime;

        if (_timeMaxBeforeHunt <= 0f)
        {
            controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Hunt));
        }
        else if (_idleTime <= 0f)
        {
            var ec = (EnemyController)controller;

            //if (ec.PlayerHunted != null)
            {
                //var dir = (ec.PlayerHunted.Position - controller.Character.Position).ToVector2(); //inverse
                var dir = _random.NextVector2(Vector2.Zero, Vector2.One);
                dir.Normalize();
                controller.Character.CurrentDirection = Character.GetCharacterDirectionFromVector2(dir);
                controller.Character.SetAnimation(Character.AnimationIndices.Stand);
            }

            ComputeIdleTime();
        }
    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }

    private void ComputeIdleTime()
    {
        _idleTime = _random.NextFloat(2.0f, 4.0f);
    }
}