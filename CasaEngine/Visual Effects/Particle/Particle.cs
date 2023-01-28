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



        /// <summary>
        /// Remove the particle?
        /// </summary>
        public bool Remove
        {
            get;
            set;
        }





        /// <summary>
        /// 
        /// </summary>
        public void Initialize()
        {
            Remove = false;
        }

    }
}
