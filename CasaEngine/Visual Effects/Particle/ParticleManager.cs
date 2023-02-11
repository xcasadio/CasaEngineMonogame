namespace CasaEngine.Particle
{
    public class ParticleManager
    {

        private List<ParticleSystem> _particleSystem = new();
        private Stack<ParticleSystem> _freeParticleSystem = new();







        public void AddParticleSystem(ParticleSystem particleSyste)
        {
            _particleSystem.Add(particleSyste);
        }

    }
}
