namespace CasaEngine.Particle
{
    public class ParticleManager
    {

        private List<ParticleSystem> _particleSystem = new List<ParticleSystem>();
        private Stack<ParticleSystem> _freeParticleSystem = new Stack<ParticleSystem>();







        public void AddParticleSystem(ParticleSystem particleSyste)
        {
            _particleSystem.Add(particleSyste);
        }

    }
}
