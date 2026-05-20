using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;

namespace CasaEngine.Framework.Particles.Runtime;

public sealed class ParticleEmitterRuntime
{
    private readonly int[] _aliveParticleIndices;
    private readonly int[] _freeParticleIndices;
    private readonly int[] _aliveSlotsByParticleIndex;
    private int _aliveCount;
    private int _freeCount;
    private float _simulationSpeed = 1.0f;

    public ParticleEmitterDefinition Definition { get; }

    public Particle[] Particles { get; }

    public int Capacity => Particles.Length;

    public int AliveCount => _aliveCount;

    public ReadOnlySpan<int> AliveParticleIndices => _aliveParticleIndices.AsSpan(0, _aliveCount);

    public ParticlePlaybackState PlaybackState { get; private set; } = ParticlePlaybackState.Stopped;

    public bool Looping { get; set; }

    public float SimulationSpeed
    {
        get => _simulationSpeed;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Simulation speed must be finite and non-negative.");
            }

            _simulationSpeed = value;
        }
    }

    public float PlaybackTime { get; private set; }

    public float CycleTime { get; private set; }

    public int CycleIndex { get; private set; }

    public bool HasStarted => PlaybackTime >= Definition.StartDelay;

    public bool IsAlive => PlaybackState != ParticlePlaybackState.Stopped || _aliveCount > 0;

    public ParticleEmitterRuntime(ParticleEmitterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.MaxParticles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Emitter max particles must be greater than zero.");
        }

        Definition = definition;
        Looping = definition.Looping;
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

    public void Play()
    {
        if (PlaybackState == ParticlePlaybackState.Playing)
        {
            return;
        }

        PlaybackState = ParticlePlaybackState.Playing;
    }

    public void Pause()
    {
        if (PlaybackState == ParticlePlaybackState.Playing)
        {
            PlaybackState = ParticlePlaybackState.Paused;
        }
    }

    public void Stop(bool clearParticles)
    {
        PlaybackState = ParticlePlaybackState.Stopped;
        PlaybackTime = 0.0f;
        CycleTime = 0.0f;
        CycleIndex = 0;

        if (clearParticles)
        {
            Clear();
        }
    }

    public void Restart(bool clearParticles = true)
    {
        Stop(clearParticles);
        Play();
    }

    public bool AdvancePlayback(float elapsedSeconds)
    {
        if (PlaybackState != ParticlePlaybackState.Playing || elapsedSeconds <= 0.0f || _simulationSpeed <= 0.0f)
        {
            return false;
        }

        float scaledElapsedSeconds = elapsedSeconds * _simulationSpeed;
        PlaybackTime += scaledElapsedSeconds;

        float startDelay = Definition.StartDelay;
        if (PlaybackTime < startDelay)
        {
            CycleTime = 0.0f;
            return false;
        }

        float duration = Definition.Duration;
        float activeTime = PlaybackTime - startDelay;
        if (Looping)
        {
            if (duration <= 0.0f)
            {
                CycleTime = 0.0f;
                CycleIndex = 0;
                return true;
            }

            CycleIndex = (int)MathF.Floor(activeTime / duration);
            CycleTime = activeTime - CycleIndex * duration;
            return true;
        }

        if (activeTime >= duration)
        {
            CycleTime = duration;
            PlaybackState = ParticlePlaybackState.Stopped;
            return false;
        }

        CycleIndex = 0;
        CycleTime = activeTime;
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