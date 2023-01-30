namespace CasaEngine.Gameplay
{
    public class TeamInfo
    {

        public Color m_Color;
        public int Numero;
        public bool AllowFriendlyDamage = false;







        public bool CanAttack(TeamInfo teamInfo_)
        {
            return !(teamInfo_.Numero == Numero && teamInfo_.AllowFriendlyDamage == false);
        }

    }
}
