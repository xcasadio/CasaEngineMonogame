using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;

namespace RPGDemo.Controllers;

public class AiController : Controller
{
    public enum AiState
    {
        Idle,
        ChooseNextMove,
        Attack,
        ReactToAttack,

        Evade,
        Block,
        Counter,
    }

    //use in MoveToState, the position where the character move to
    public Vector2 TargetPosition;

    public AiController(Character character) : base(character)
    {
        /*m_States.Add(AIState.Idle, new AIState_Idle());
        m_States.Add(AIState.ChooseNextMove, new AIState_ChooseNextMove());
        m_States.Add(AIState.Attack, new AIState_Attack());*/

        //StateMachine.CurrentState = GetState(AIState.Idle);
    }

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);
    }

    public override void Update(float elapsedTime)
    {
        base.Update(elapsedTime);
    }
}