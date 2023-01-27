using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Gameplay.Actor
{
    /// <summary>
    /// behaviour for Text2D
    /// Example : movement or deletion
    /// </summary>
    public abstract class Text2DBehaviour
    {
        #region Fields

        Text2DActor2D m_Text2D;

        #endregion

        #region Properties

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public abstract void Update(float elapsedTime_);

        #endregion
    }
}
