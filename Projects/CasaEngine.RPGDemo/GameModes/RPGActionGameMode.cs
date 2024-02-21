using CasaEngine.Framework.GameFramework;

namespace CasaEngine.RPGDemo.GameModes
{
    public class RPGActionGameMode : GameMode
    {
        protected override bool ReadyToEndMatch()
        {
            //if player is dead
            //return true;

            return false;
        }
    }
}
