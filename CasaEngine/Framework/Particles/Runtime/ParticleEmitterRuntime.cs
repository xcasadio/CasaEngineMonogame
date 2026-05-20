using CasaEngine.Framework.Particles.Authoring;

namespace CasaEngine.Framework.Particles.Runtime;

public sealed class ParticleEmitterRuntime
{
    private readonly int[] _aliveParticleIndices;
    private readonly int[] _freeParticleIndices;
    private readonly int[] _aliveSlotsByParticleIndex;
    private int _aliveCount;
    private int _freeCount;

    public ParticleEmitterDefinition Definition { get; }

    public Particle[] Particles { get; }

    public int Capacity => Particles.Length;

    public int AliveCount => _aliveCount;

    public ReadOnlySpan<int> AliveParticleIndices => _aliveParticleIndices.AsSpan(0, _aliveCount);

    public ParticleEmitterRuntime(ParticleEmitterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.MaxParticles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Emitter max particles must be greater than zero.");
        }

        Definition = definition;
        Particles = new Particle[definition.MaxParticles];
        _aliveParticleIndices = new int[definition.MaxParticles];
        _freeParticleIndices = new int[definition.MaxParticles];
        _aliveSlotsByParticleIndex = new int[definition.MaxParticles];

        ResetFreeList();
    }

    public ref Particle GetParticle(int index)
    {
        if ((uint)index >= (uint)Particles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref Particles[index];
    }

    public bool TrySpawn(out int particleIndex)
    {
        if (_freeCount == 0)
        {
            particleIndex = -1;
            return false;
        }

        _freeCount--;
        particleIndex = _freeParticleIndices[_freeCount];

        int aliveSlot = _aliveCount;
        _aliveCount++;
        _aliveParticleIndices[aliveSlot] = particleIndex;
        _aliveSlotsByParticleIndex[particleIndex] = aliveSlot;

        ref Particle particle = ref Particles[particleIndex];
        particle = default;
        particle.IsAlive = true;
        particle.Alpha = 1.0f;
        return true;
    }

    public bool Kill(int particleIndex)
    {
        if ((uint)particleIndex >= (uint)Particles.Length || !Particles[particleIndex].IsAlive)
        {
            return false;
        }

        int removedSlot = _aliveSlotsByParticleIndex[particleIndex];
        int lastAliveSlot = _aliveCount - 1;
        int lastParticleIndex = _aliveParticleIndices[lastAliveSlot];

        _aliveParticleIndices[removedSlot] = lastParticleIndex;
        _aliveSlotsByParticleIndex[lastParticleIndex] = removedSlot;
        _aliveParticleIndices[lastAliveSlot] = 0;
        _aliveSlotsByParticleIndex[particleIndex] = -1;
        _aliveCount--;

        Particles[particleIndex] = default;
        _freeParticleIndices[_freeCount] = particleIndex;
        _freeCount++;
        return true;
    }

    public void Clear()
    {
        for (int aliveIndex = 0; aliveIndex < _aliveCount; aliveIndex++)
        {
            int particleIndex = _aliveParticleIndices[aliveIndex];
            Particles[particleIndex] = default;
        }

        ResetFreeList();
    }

    private void ResetFreeList()
    {
        _aliveCount = 0;
        _freeCount = Particles.Length;

        for (int index = 0; index < Particles.Length; index++)
        {
            _aliveParticleIndices[index] = 0;
            _freeParticleIndices[index] = Particles.Length - 1 - index;
            _aliveSlotsByParticleIndex[index] = -1;
        }
    }
}