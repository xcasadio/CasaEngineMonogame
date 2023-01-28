using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    public class TeamInfo
    {

        public Color m_Color;
        public int Numero;
        public bool AllowFriendlyDamage = false;







        /// <summary>
        /// 
        /// </summary>
        /// <param name="teamInfo_"></param>
        /// <returns></returns>
        public bool CanAttack(TeamInfo teamInfo_)
        {
            return !(teamInfo_.Numero == Numero && teamInfo_.AllowFriendlyDamage == false);
        }

    }
}
