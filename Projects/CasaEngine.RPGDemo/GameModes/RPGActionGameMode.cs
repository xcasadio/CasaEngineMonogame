using CasaEngine.Framework.Gameplay;

namespace CasaEngine.RPGDemo.GameModes
{
    public class RPGActionGameMode : GameplayMode
    {
        public override GameplayResult EvaluateResult()
        {
            //if player is dead
            //return GameplayResult.Failure;

            return GameplayResult.Running;
        }
    }
}
