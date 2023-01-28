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

        private List<ParticleSystem> m_ParticleSystem = new List<ParticleSystem>();
        private Stack<ParticleSystem> m_FreeParticleSystem = new Stack<ParticleSystem>();







        /// <summary>
        /// 
        /// </summary>
        /// <param name="particleSystem_"></param>
        public void AddParticleSystem(ParticleSystem particleSystem_)
        {
            m_ParticleSystem.Add(particleSystem_);
        }

    }
}
