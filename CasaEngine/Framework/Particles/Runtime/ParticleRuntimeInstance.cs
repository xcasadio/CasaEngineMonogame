using CasaEngine.Core.Math.Geometry;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace CasaEngine.Framework.Particles.Runtime;

public sealed class ParticleRuntimeInstance
{
    private float _simulationSpeed = 1.0f;
    private float _emissionScale = 1.0f;

    public ParticleEffectAsset Asset { get; }

    public ParticleEmitterRuntime[] Emitters { get; }

    public Matrix WorldMatrix { get; set; } = Matrix.Identity;

    public bool HasBounds { get; private set; }

    public BoundingBox Bounds { get; private set; }

    public ParticleRuntimeMetrics Metrics { get; private set; } = ParticleRuntimeMetrics.Empty;

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

    public float EmissionScale
    {
        get => _emissionScale;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Emission scale must be finite and non-negative.");
            }

            _emissionScale = value;
            for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
            {
                Emitters[emitterIndex].EmissionScale = value;
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

        RefreshMetrics(0.0);
    }

    public void Clear()
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Clear();
        }

        HasBounds = false;
        Bounds = default;
        RefreshMetrics(0.0);
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

        RefreshMetrics(0.0);
    }

    public void Restart(bool clearParticles = true)
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Restart(clearParticles);
        }
    }

    public int Emit(int particleCount)
    {
        int emittedCount = 0;
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].WorldMatrix = WorldMatrix;
            emittedCount += Emitters[emitterIndex].Emit(particleCount);
        }

        UpdateBounds();
        RefreshMetrics(0.0);
        return emittedCount;
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
        long startTimestamp = Stopwatch.GetTimestamp();
        int emittedCount = 0;
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].WorldMatrix = WorldMatrix;
            emittedCount += Emitters[emitterIndex].UpdateEmission(elapsedSeconds);
        }

        RefreshMetrics(GetElapsedMilliseconds(startTimestamp));
        return emittedCount;
    }

    public int Update(float elapsedSeconds)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        int emittedCount = 0;
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].WorldMatrix = WorldMatrix;
            emittedCount += Emitters[emitterIndex].Update(elapsedSeconds);
        }

        UpdateBounds();
        RefreshMetrics(GetElapsedMilliseconds(startTimestamp));
        return emittedCount;
    }

    public void UpdateBounds()
    {
        BoundingBox bounds = BoundingBoxHelper.Create();
        HasBounds = false;

        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            ParticleEmitterRuntime emitter = Emitters[emitterIndex];
            emitter.UpdateBounds(WorldMatrix);
            if (!emitter.HasBounds)
            {
                continue;
            }

            bounds.ExpandBy(emitter.WorldBounds);
            HasBounds = true;
        }

        Bounds = HasBounds ? bounds : default;
    }

    private void RefreshMetrics(double simulationCpuMilliseconds)
    {
        int capacity = 0;
        int aliveCount = 0;
        int lastEmittedCount = 0;
        int lastKilledCount = 0;
        int maxAliveCountReached = 0;
        bool maxReached = false;

        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            ParticleEmitterRuntime emitter = Emitters[emitterIndex];
            capacity += emitter.Capacity;
            aliveCount += emitter.AliveCount;
            lastEmittedCount += emitter.LastEmittedCount;
            lastKilledCount += emitter.LastKilledCount;
            maxAliveCountReached += emitter.MaxAliveCountReached;
            maxReached |= emitter.MaxReached;
        }

        Metrics = new ParticleRuntimeMetrics(
            capacity,
            aliveCount,
            capacity - aliveCount,
            lastEmittedCount,
            lastKilledCount,
            maxAliveCountReached,
            maxReached,
            simulationCpuMilliseconds);
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
        => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
}