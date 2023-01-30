namespace CasaEngine.Particle
{
    public class ParticleManager
    {

        private List<ParticleSystem> m_ParticleSystem = new List<ParticleSystem>();
        private Stack<ParticleSystem> m_FreeParticleSystem = new Stack<ParticleSystem>();







        public void AddParticleSystem(ParticleSystem particleSystem_)
        {
            m_ParticleSystem.Add(particleSystem_);
        }

    }
}
