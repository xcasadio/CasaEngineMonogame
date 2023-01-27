using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Particle
{
    /// <summary>
    /// 
    /// </summary>
    public class Particle
    {
        #region Fields

        #endregion

        #region Properties

        /// <summary>
        /// Remove the particle?
        /// </summary>
        public bool Remove
        {
            get;
            set;
        }

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        public void Initialize()
        {
            Remove = false;
        }

        #endregion
    }
}
