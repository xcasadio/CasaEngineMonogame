namespace CasaEngine.Gameplay
{
    public class TeamInfo
    {

        public Color Color;
        public int Numero;
        public bool AllowFriendlyDamage = false;







        public bool CanAttack(TeamInfo teamInfo)
        {
            return !(teamInfo.Numero == Numero && teamInfo.AllowFriendlyDamage == false);
        }

    }
}
