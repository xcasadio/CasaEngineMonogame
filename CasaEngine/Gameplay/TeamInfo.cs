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
        #region Fields

        public Color m_Color;
        public int Numero;
        public bool AllowFriendlyDamage = false;

        #endregion

        #region Properties

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="teamInfo_"></param>
        /// <returns></returns>
        public bool CanAttack(TeamInfo teamInfo_)
        {
            return !(teamInfo_.Numero == Numero && teamInfo_.AllowFriendlyDamage == false);
        }

        #endregion
    }
}
