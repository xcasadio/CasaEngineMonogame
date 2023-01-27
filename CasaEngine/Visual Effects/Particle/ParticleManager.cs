using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Particle
{
    /// <summary>
    /// 
    /// </summary>
    public class ParticleManager
    {
        #region Fields

        private List<ParticleSystem> m_ParticleSystem = new List<ParticleSystem>();
        private Stack<ParticleSystem> m_FreeParticleSystem = new Stack<ParticleSystem>();

        #endregion

        #region Properties

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="particleSystem_"></param>
        public void AddParticleSystem(ParticleSystem particleSystem_)
        {
            m_ParticleSystem.Add(particleSystem_);
        }

        #endregion
    }
}
