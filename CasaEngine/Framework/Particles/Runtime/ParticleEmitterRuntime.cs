using CasaEngine.Core.Math.Geometry;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Runtime;

public sealed class ParticleEmitterRuntime
{
    private readonly int[] _aliveParticleIndices;
    private readonly int[] _freeParticleIndices;
    private readonly int[] _aliveSlotsByParticleIndex;
    private readonly uint _randomSeed;
    private int _aliveCount;
    private int _freeCount;
    private float _simulationSpeed = 1.0f;
    private float _emissionScale = 1.0f;
    private float _emissionAccumulator;
    private ParticleRandom _random;

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
        }
    }

    public float PlaybackTime { get; private set; }

    public float CycleTime { get; private set; }

    public int CycleIndex { get; private set; }

    public bool HasStarted => PlaybackTime >= Definition.StartDelay;

    public bool IsAlive => PlaybackState != ParticlePlaybackState.Stopped || _aliveCount > 0;

    public bool AlwaysVisible => Definition.Renderer.AlwaysVisible;

    public Matrix WorldMatrix { get; set; } = Matrix.Identity;

    public bool HasBounds { get; private set; }

    public BoundingBox LocalBounds { get; private set; }

    public BoundingBox WorldBounds { get; private set; }

    public ParticleEmitterRuntime(ParticleEmitterDefinition definition, uint randomSeed = 1u)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.MaxParticles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Emitter max particles must be greater than zero.");
        }

        Definition = definition;
        _randomSeed = randomSeed == 0u ? 1u : randomSeed;
        _random = new ParticleRandom(_randomSeed);
        Looping = definition.Looping;
        Particles = new Particle[definition.MaxParticles];
        _aliveParticleIndices = new int[definition.MaxParticles];
        _freeParticleIndices = new int[definition.MaxParticles];
        _aliveSlotsByParticleIndex = new int[definition.MaxParticles];

        ResetFreeList();
        ResetBounds();
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

    public int UpdateEmission(float elapsedSeconds)
    {
        if (PlaybackState != ParticlePlaybackState.Playing || elapsedSeconds <= 0.0f || _simulationSpeed <= 0.0f)
        {
            return 0;
        }

        float previousPlaybackTime = PlaybackTime;
        AdvancePlayback(elapsedSeconds);

        float previousActiveTime = previousPlaybackTime - Definition.StartDelay;
        float currentActiveTime = PlaybackTime - Definition.StartDelay;
        int emittedCount = EmitContinuous(previousActiveTime, currentActiveTime);
        emittedCount += EmitBursts(previousActiveTime, currentActiveTime);
        return emittedCount;
    }

    public int Emit(int particleCount)
    {
        int emittedCount = 0;
        for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
        {
            if (!TrySpawn(out _))
            {
                break;
            }

            InitializeParticle(_aliveParticleIndices[_aliveCount - 1]);
            emittedCount++;
        }

        return emittedCount;
    }

    public int Update(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0.0f || _simulationSpeed <= 0.0f)
        {
            return 0;
        }

        int emittedCount = UpdateEmission(elapsedSeconds);
        if (PlaybackState != ParticlePlaybackState.Paused && _aliveCount > 0)
        {
            SimulateParticles(elapsedSeconds * _simulationSpeed);
        }

        return emittedCount;
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
        ResetEmissionState();

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
        ResetBounds();
    }

    public void UpdateBounds(Matrix worldMatrix)
    {
        WorldMatrix = worldMatrix;
        if (_aliveCount == 0)
        {
            ResetBounds();
            return;
        }

        BoundingBox bounds = BoundingBoxHelper.Create();
        for (int aliveIndex = 0; aliveIndex < _aliveCount; aliveIndex++)
        {
            Particle particle = Particles[_aliveParticleIndices[aliveIndex]];
            float radius = MathF.Max(particle.Size.X, particle.Size.Y) * 0.5f;
            Vector3 extent = new(radius, radius, radius);
            bounds.ExpandBy(particle.Position - extent);
            bounds.ExpandBy(particle.Position + extent);
        }

        HasBounds = bounds.Valid();
        if (!HasBounds)
        {
            ResetBounds();
            return;
        }

        LocalBounds = bounds;
        WorldBounds = Definition.Simulation.SimulationSpace == ParticleSimulationSpace.Local
            ? bounds.Transform(worldMatrix)
            : bounds;
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

    private int EmitContinuous(float previousActiveTime, float currentActiveTime)
    {
        float activeDeltaSeconds = GetActiveDeltaSeconds(previousActiveTime, currentActiveTime);
        float scaledRate = Definition.Emission.RateOverTime * _emissionScale;
        if (activeDeltaSeconds <= 0.0f || scaledRate <= 0.0f)
        {
            return 0;
        }

        _emissionAccumulator += activeDeltaSeconds * scaledRate;
        int particlesToEmit = (int)_emissionAccumulator;
        if (particlesToEmit <= 0)
        {
            return 0;
        }

        _emissionAccumulator -= particlesToEmit;
        return Emit(particlesToEmit);
    }

    private int EmitBursts(float previousActiveTime, float currentActiveTime)
    {
        if (Definition.Emission.Bursts.Count == 0 || currentActiveTime < 0.0f)
        {
            return 0;
        }

        if (Looping)
        {
            float duration = Definition.Duration;
            if (duration <= 0.0f)
            {
                return 0;
            }

            int emittedCount = 0;
            float lastActiveTime = MathF.Max(0.0f, currentActiveTime);
            int firstCycleIndex = previousActiveTime <= 0.0f ? 0 : (int)MathF.Floor(previousActiveTime / duration);
            int lastCycleIndex = (int)MathF.Floor(lastActiveTime / duration);
            for (int cycleIndex = firstCycleIndex; cycleIndex <= lastCycleIndex; cycleIndex++)
            {
                emittedCount += EmitBurstsForCycle(previousActiveTime, lastActiveTime, cycleIndex * duration, duration);
            }

            return emittedCount;
        }

        float clampedCurrentTime = MathHelper.Clamp(currentActiveTime, 0.0f, Definition.Duration);
        return EmitBurstsForCycle(previousActiveTime, clampedCurrentTime, 0.0f, Definition.Duration);
    }

    private int EmitBurstsForCycle(float previousActiveTime, float currentActiveTime, float cycleStartTime, float duration)
    {
        int emittedCount = 0;
        for (int burstIndex = 0; burstIndex < Definition.Emission.Bursts.Count; burstIndex++)
        {
            ParticleBurst burst = Definition.Emission.Bursts[burstIndex];
            if (burst.Time < 0.0f || burst.Time > duration)
            {
                continue;
            }

            float triggerTime = cycleStartTime + burst.Time;
            if (!ShouldTriggerBurst(previousActiveTime, currentActiveTime, triggerTime))
            {
                continue;
            }

            int scaledCount = (int)(burst.SampleCount(ref _random) * _emissionScale);
            if (scaledCount > 0)
            {
                emittedCount += Emit(scaledCount);
            }
        }

        return emittedCount;
    }

    private float GetActiveDeltaSeconds(float previousActiveTime, float currentActiveTime)
    {
        float previousTime = MathF.Max(0.0f, previousActiveTime);
        float currentTime = MathF.Max(0.0f, currentActiveTime);
        if (!Looping)
        {
            previousTime = MathHelper.Clamp(previousTime, 0.0f, Definition.Duration);
            currentTime = MathHelper.Clamp(currentTime, 0.0f, Definition.Duration);
        }

        return currentTime - previousTime;
    }

    private static bool ShouldTriggerBurst(float previousActiveTime, float currentActiveTime, float triggerTime)
    {
        if (triggerTime == 0.0f && previousActiveTime <= 0.0f)
        {
            return currentActiveTime >= 0.0f;
        }

        return triggerTime > previousActiveTime && triggerTime <= currentActiveTime;
    }

    private void ResetEmissionState()
    {
        _emissionAccumulator = 0.0f;
        _random = new ParticleRandom(_randomSeed);
    }

    private void InitializeParticle(int particleIndex)
    {
        ref Particle particle = ref Particles[particleIndex];
        ParticleShapeSampler.Sample(Definition.Shape, ref _random, out Vector3 localPosition, out Vector3 direction);

        float lifetime = Definition.Initial.Lifetime.Sample(ref _random);
        float speed = Definition.Initial.Speed.Sample(ref _random);
        Vector2 size = Definition.Initial.Size.Sample(ref _random);
        Color startColor = Definition.Initial.StartColor.Evaluate(0.0f);

        if (Definition.Simulation.SimulationSpace == ParticleSimulationSpace.World)
        {
            localPosition = Vector3.Transform(localPosition, WorldMatrix);
            direction = NormalizeOrFallback(Vector3.TransformNormal(direction, WorldMatrix), direction);
        }

        particle.Age = 0.0f;
        particle.Lifetime = MathF.Max(0.0001f, lifetime);
        particle.Position = localPosition;
        particle.InitialVelocity = direction * speed;
        particle.Velocity = particle.InitialVelocity;
        particle.StartSize = size;
        particle.Size = size;
        particle.Rotation = MathHelper.ToRadians(Definition.Initial.Rotation.Sample(ref _random));
        particle.AngularVelocity = MathHelper.ToRadians(Definition.Initial.AngularVelocity.Sample(ref _random));
        particle.StartColor = startColor;
        particle.Color = startColor;
        particle.Alpha = startColor.A / 255.0f;
        ParticleFlipbookModule flipbook = Definition.Renderer.Flipbook;
        particle.FlipbookStartFrame = flipbook != null && flipbook.RandomStartFrame
            ? _random.NextInt(0, flipbook.EffectiveFrameCount - 1)
            : 0;
        particle.FlipbookFrame = ResolveFlipbookFrame(flipbook, particle.FlipbookStartFrame, particle.Age, 0.0f);
    }

    private void SimulateParticles(float elapsedSeconds)
    {
        int aliveIndex = 0;
        while (aliveIndex < _aliveCount)
        {
            int particleIndex = _aliveParticleIndices[aliveIndex];
            if (SimulateParticle(particleIndex, elapsedSeconds))
            {
                aliveIndex++;
            }
        }
    }

    private bool SimulateParticle(int particleIndex, float elapsedSeconds)
    {
        ref Particle particle = ref Particles[particleIndex];
        particle.Age += elapsedSeconds;
        if (particle.Age >= particle.Lifetime)
        {
            Kill(particleIndex);
            return false;
        }

        float normalizedLifetime = MathHelper.Clamp(particle.Age / particle.Lifetime, 0.0f, 1.0f);
        Vector3 acceleration = Definition.Simulation.Gravity * Definition.Simulation.GravityScale;
        particle.Velocity += acceleration * elapsedSeconds;

        float drag = Definition.Simulation.Drag;
        if (drag > 0.0f)
        {
            particle.Velocity *= MathF.Max(0.0f, 1.0f - drag * elapsedSeconds);
        }

        float velocityScale = Definition.Simulation.VelocityOverLifetime.Evaluate(normalizedLifetime);
        particle.Position += particle.Velocity * velocityScale * elapsedSeconds;
        particle.Rotation += particle.AngularVelocity * elapsedSeconds;
        particle.Size = particle.StartSize * Definition.Simulation.SizeOverLifetime.Evaluate(normalizedLifetime);
        particle.Color = MultiplyColors(particle.StartColor, Definition.Simulation.ColorOverLifetime.Evaluate(normalizedLifetime));
        particle.Alpha = (particle.Color.A / 255.0f) * Definition.Simulation.AlphaOverLifetime.Evaluate(normalizedLifetime);
        particle.FlipbookFrame = ResolveFlipbookFrame(Definition.Renderer.Flipbook, particle.FlipbookStartFrame, particle.Age, normalizedLifetime);
        return true;
    }

    internal static int ResolveFlipbookFrame(ParticleFlipbookModule flipbook, int startFrame, float ageSeconds, float normalizedLifetime)
    {
        if (flipbook == null)
        {
            return 0;
        }

        int frameCount = flipbook.EffectiveFrameCount;
        if (frameCount <= 1)
        {
            return 0;
        }

        int startFrameOffset = NormalizeFrame(startFrame, frameCount);
        if (flipbook.FramesPerSecond > 0.0f)
        {
            float safeAgeSeconds = float.IsNaN(ageSeconds) || float.IsInfinity(ageSeconds)
                ? 0.0f
                : MathF.Max(0.0f, ageSeconds);
            int frameAdvance = (int)MathF.Floor(safeAgeSeconds * flipbook.FramesPerSecond);
            return NormalizeFrame(startFrameOffset + frameAdvance, frameCount);
        }

        float curveValue = flipbook.FrameOverLifetime?.Evaluate(normalizedLifetime) ?? 0.0f;
        if (float.IsNaN(curveValue) || float.IsInfinity(curveValue))
        {
            curveValue = 0.0f;
        }

        int curveFrame = (int)MathF.Floor(MathHelper.Clamp(curveValue, 0.0f, 1.0f) * frameCount);
        if (curveFrame >= frameCount)
        {
            curveFrame = frameCount - 1;
        }

        return Math.Clamp(startFrameOffset + curveFrame, 0, frameCount - 1);
    }

    private static int NormalizeFrame(int frame, int frameCount)
    {
        int normalizedFrame = frame % frameCount;
        return normalizedFrame < 0 ? normalizedFrame + frameCount : normalizedFrame;
    }

    private static Color MultiplyColors(Color first, Color second)
    {
        return new Color(
            (byte)(first.R * second.R / 255),
            (byte)(first.G * second.G / 255),
            (byte)(first.B * second.B / 255),
            (byte)(first.A * second.A / 255));
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared <= 0.000001f)
        {
            return fallback;
        }

        return value / MathF.Sqrt(lengthSquared);
    }

    private void ResetBounds()
    {
        HasBounds = false;
        LocalBounds = default;
        WorldBounds = default;
    }
}