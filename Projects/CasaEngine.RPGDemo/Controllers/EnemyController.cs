using CasaEngine.Framework.Game;
using CasaEngine.RPGDemo.Controllers.EnemyState;
using static CasaEngine.RPGDemo.Controllers.Character;

namespace CasaEngine.RPGDemo.Controllers;

public class EnemyController : AiController
{
    public Character PlayerHunted { get; set; }

    public EnemyController(Character character)
        : base(character)
    {
        //character.IsPLayer = false;
    }

    public override void InitializePrivate(CasaEngineGame game)
    {
        base.InitializePrivate(game);

        StateMachine.GlobalState = new EnemyGlobalState();
        AddState((int)EnemyControllerState.Idle, new EnemyIdleState());
        AddState((int)EnemyControllerState.Hit, new EnemyHitState());
        AddState((int)EnemyControllerState.MoveTo, new EnemyMoveToState());
        AddState((int)EnemyControllerState.Hunt, new EnemyHuntState());
        AddState((int)EnemyControllerState.Attack, new EnemyAttackState());
        AddState((int)EnemyControllerState.Dying, new EnemyDyingState());
        StateMachine.Transition(GetState((int)EnemyControllerState.Idle));
        StateMachine.GlobalState = new EnemyGlobalState();

        Character.CurrentDirection = Character2dDirection.Down;
        Character.SetAnimation(AnimationIndices.Stand);
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);

        /*Vector2 dir;
        Character2dDirection cDir;

        if (s == 0)
        {
            dir = Vector2.UnitY;
            cDir = Character2dDirection.Down;

            if (Character.Position.Y > GameInfo.Instance.Game.GraphicsDevice.Viewport.Height - 50)
            {
                s = 1;
                Character.Animation2DPlayer.SetCurrentAnimationByID((int)AnimationIndex.WalkUp);
            }
        }
        else
        {
            dir = -Vector2.UnitY;
            cDir = Character2dDirection.Up;

            if (Character.Position.Y < 50)
            {
                s = 0;
                Character.Animation2DPlayer.SetCurrentAnimationByID((int)AnimationIndex.WalkDown);
            }
        }

        Character.Move(ref dir);
        Character.CurrentDirection = cDir;*/
    }
}