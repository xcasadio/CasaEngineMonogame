using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Runtime;

internal interface IParticleEmitterInitializeModule
{
    void Initialize(Matrix worldMatrix, ref ParticleRandom random, ref Particle particle);
}

internal interface IParticleEmitterUpdateModule
{
    bool Update(ref Particle particle, float elapsedSeconds);
}

internal sealed class FixedParticleEmitterRuntimeModules : IParticleEmitterInitializeModule, IParticleEmitterUpdateModule
{
    private readonly ParticleEmitterDefinition _definition;

    public FixedParticleEmitterRuntimeModules(ParticleEmitterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = definition;
    }

    public void Initialize(Matrix worldMatrix, ref ParticleRandom random, ref Particle particle)
    {
        ParticleShapeSampler.Sample(_definition.Shape, ref random, out Vector3 localPosition, out Vector3 direction);

        float lifetime = _definition.Initial.Lifetime.Sample(ref random);
        float speed = _definition.Initial.Speed.Sample(ref random);
        Vector2 size = _definition.Initial.Size.Sample(ref random);
        Color startColor = _definition.Initial.StartColor.Evaluate(0.0f);

        if (_definition.Simulation.SimulationSpace == ParticleSimulationSpace.World)
        {
            localPosition = Vector3.Transform(localPosition, worldMatrix);
            direction = NormalizeOrFallback(Vector3.TransformNormal(direction, worldMatrix), direction);
        }

        particle.Age = 0.0f;
        particle.Lifetime = MathF.Max(0.0001f, lifetime);
        particle.Position = localPosition;
        particle.InitialVelocity = direction * speed;
        particle.Velocity = particle.InitialVelocity;
        particle.StartSize = size;
        particle.Size = size;
        particle.Rotation = MathHelper.ToRadians(_definition.Initial.Rotation.Sample(ref random));
        particle.AngularVelocity = MathHelper.ToRadians(_definition.Initial.AngularVelocity.Sample(ref random));
        particle.StartColor = startColor;
        particle.Color = startColor;
        particle.Alpha = startColor.A / 255.0f;

        ParticleFlipbookModule flipbook = _definition.Renderer.Flipbook;
        particle.FlipbookStartFrame = flipbook != null && flipbook.RandomStartFrame
            ? random.NextInt(0, flipbook.EffectiveFrameCount - 1)
            : 0;
        particle.FlipbookFrame = ParticleEmitterRuntime.ResolveFlipbookFrame(flipbook, particle.FlipbookStartFrame, particle.Age, 0.0f);
    }

    public bool Update(ref Particle particle, float elapsedSeconds)
    {
        particle.Age += elapsedSeconds;
        if (particle.Age >= particle.Lifetime)
        {
            return false;
        }

        float normalizedLifetime = MathHelper.Clamp(particle.Age / particle.Lifetime, 0.0f, 1.0f);
        Vector3 acceleration = _definition.Simulation.Gravity * _definition.Simulation.GravityScale;
        particle.Velocity += acceleration * elapsedSeconds;

        float drag = _definition.Simulation.Drag;
        if (drag > 0.0f)
        {
            particle.Velocity *= MathF.Max(0.0f, 1.0f - drag * elapsedSeconds);
        }

        float velocityScale = _definition.Simulation.VelocityOverLifetime.Evaluate(normalizedLifetime);
        particle.Position += particle.Velocity * velocityScale * elapsedSeconds;
        particle.Rotation += particle.AngularVelocity * elapsedSeconds;
        particle.Size = particle.StartSize * _definition.Simulation.SizeOverLifetime.Evaluate(normalizedLifetime);
        particle.Color = MultiplyColors(particle.StartColor, _definition.Simulation.ColorOverLifetime.Evaluate(normalizedLifetime));
        particle.Alpha = (particle.Color.A / 255.0f) * _definition.Simulation.AlphaOverLifetime.Evaluate(normalizedLifetime);
        particle.FlipbookFrame = ParticleEmitterRuntime.ResolveFlipbookFrame(_definition.Renderer.Flipbook, particle.FlipbookStartFrame, particle.Age, normalizedLifetime);
        return true;
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
}