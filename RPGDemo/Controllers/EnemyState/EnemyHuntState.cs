using CasaEngine.Core.Helpers;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.AI.StateMachines;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers.EnemyState;

public class EnemyHuntState : IState<Controller>
{
    public string Name => "Enemy Hunt State";

    public virtual void Enter(Controller controller)
    {
        var joyDir = Vector2.Zero;
        controller.Character.Move(ref joyDir);

        //if (controller is EnemyController enemyController)
        //{
        //    Character playerHunted = null;
        //    float distance = float.MaxValue;
        //
        //    foreach (Character player in GameInfo.Instance.WorldInfo.GetPlayers())
        //    {
        //        if (playerHunted == null)
        //        {
        //            playerHunted = player;
        //        }
        //
        //        float f = (player.Position - enemyController.Character.Position).LengthSquared();
        //
        //        if (f < distance)
        //        {
        //            distance = f;
        //            playerHunted = player;
        //        }
        //    }
        //
        //    enemyController.PlayerHunted = playerHunted;
        //
        //    if (playerHunted == null)
        //    {
        //        //Bug create a infinite loop
        //        //controller.StateMachine.Transition(controller.GetState((int)EnemyControllerState.Idle));
        //    }
        //}
    }

    public virtual void Exit(Controller controller)
    {

    }

    public virtual void Update(Controller controller, float elapsedTime)
    {
        //move to player
        if (controller is EnemyController enemyController)
        {
            var dir = (enemyController.PlayerHunted.Position - enemyController.Character.Position).ToVector2();
            var l = dir.LengthSquared();
            dir.Normalize();

            if (l <= 100 * 100
                /* && raycast no object between them*/)
            {
                //to delete
                /*dir = Vector2.Zero;
                controller.Character.Move(ref dir);
                controller.Character.SetAnimation(Character.AnimationIndices.Attack);*/
                enemyController.Character.CurrentDirection = Character.GetCharacterDirectionFromVector2(dir);
                enemyController.StateMachine.Transition(enemyController.GetState((int)EnemyControllerState.Attack));
            }
            else
            {
                enemyController.Character.Move(ref dir);
                enemyController.Character.CurrentDirection = Character.GetCharacterDirectionFromVector2(dir);
                enemyController.Character.SetAnimation(Character.AnimationIndices.Walk);

                //ec.TargetPosition = NodePath.Position
                //controller.StateMachine.Transition("MoveTo");
            }
        }
    }

    public virtual bool HandleMessage(Controller controller, Message message)
    {
        return false;
    }
}