using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;

namespace CasaEngine.Framework.Particles.Runtime;

public sealed class ParticleRuntimeInstance
{
    private float _simulationSpeed = 1.0f;

    public ParticleEffectAsset Asset { get; }

    public ParticleEmitterRuntime[] Emitters { get; }

    public ParticlePlaybackState PlaybackState
    {
        get
        {
            bool hasPausedEmitter = false;
            for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
            {
                ParticlePlaybackState emitterState = Emitters[emitterIndex].PlaybackState;
                if (emitterState == ParticlePlaybackState.Playing)
                {
                    return ParticlePlaybackState.Playing;
                }

                if (emitterState == ParticlePlaybackState.Paused)
                {
                    hasPausedEmitter = true;
                }
            }

            return hasPausedEmitter ? ParticlePlaybackState.Paused : ParticlePlaybackState.Stopped;
        }
    }

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
            for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
            {
                Emitters[emitterIndex].SimulationSpeed = value;
            }
        }
    }

    public bool IsAlive
    {
        get
        {
            for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
            {
                if (Emitters[emitterIndex].IsAlive)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public int AliveCount
    {
        get
        {
            int aliveCount = 0;
            for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
            {
                aliveCount += Emitters[emitterIndex].AliveCount;
            }

            return aliveCount;
        }
    }

    public ParticleRuntimeInstance(ParticleEffectAsset asset, uint randomSeed = 1u)
    {
        ArgumentNullException.ThrowIfNull(asset);

        Asset = asset;
        Emitters = new ParticleEmitterRuntime[asset.Emitters.Count];
        for (int emitterIndex = 0; emitterIndex < asset.Emitters.Count; emitterIndex++)
        {
            uint emitterSeed = randomSeed + (uint)emitterIndex * 747796405u;
            Emitters[emitterIndex] = new ParticleEmitterRuntime(asset.Emitters[emitterIndex], emitterSeed);
        }
    }

    public void Clear()
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Clear();
        }
    }

    public void Play()
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Play();
        }
    }

    public void Pause()
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Pause();
        }
    }

    public void Stop(bool clearParticles)
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Stop(clearParticles);
        }
    }

    public void Restart(bool clearParticles = true)
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Restart(clearParticles);
        }
    }

    public void AdvancePlayback(float elapsedSeconds)
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].AdvancePlayback(elapsedSeconds);
        }
    }

    public int UpdateEmission(float elapsedSeconds)
    {
        int emittedCount = 0;
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            emittedCount += Emitters[emitterIndex].UpdateEmission(elapsedSeconds);
        }

        return emittedCount;
    }
}