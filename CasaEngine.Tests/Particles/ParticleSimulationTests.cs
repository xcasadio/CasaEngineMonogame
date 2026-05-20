using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleSimulationTests
{
    [Fact]
    public void Emit_InitializesParticleFromAuthoringModules()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Initial.Lifetime = FloatRange.Constant(2.0f);
        definition.Initial.Speed = FloatRange.Constant(3.0f);
        definition.Initial.Size = Vector2Range.Constant(new Vector2(2.0f, 4.0f));
        definition.Initial.Rotation = FloatRange.Constant(90.0f);
        definition.Initial.AngularVelocity = FloatRange.Constant(45.0f);
        definition.Initial.StartColor = ColorGradient.Constant(Color.Red);
        var runtime = new ParticleEmitterRuntime(definition);

        Assert.Equal(1, runtime.Emit(1));
        Particle particle = runtime.GetParticle(0);

        Assert.Equal(2.0f, particle.Lifetime);
        Assert.Equal(new Vector3(0.0f, 3.0f, 0.0f), particle.Velocity);
        Assert.Equal(new Vector2(2.0f, 4.0f), particle.Size);
        Assert.Equal(MathHelper.PiOver2, particle.Rotation, 5);
        Assert.Equal(MathHelper.PiOver4, particle.AngularVelocity, 5);
        Assert.Equal(Color.Red, particle.Color);
        Assert.Equal(1.0f, particle.Alpha);
    }

    [Fact]
    public void Update_AdvancesMotionAndRotation()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Initial.Speed = FloatRange.Constant(2.0f);
        definition.Initial.AngularVelocity = FloatRange.Constant(90.0f);
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);

        runtime.Update(0.5f);
        Particle particle = runtime.GetParticle(0);

        Assert.Equal(0.5f, particle.Age);
        Assert.Equal(new Vector3(0.0f, 1.0f, 0.0f), particle.Position);
        Assert.Equal(MathHelper.PiOver4, particle.Rotation, 5);
    }

    [Fact]
    public void Update_AppliesGravityAndDrag()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Initial.Speed = FloatRange.Constant(0.0f);
        definition.Simulation.Gravity = new Vector3(0.0f, 4.0f, 0.0f);
        definition.Simulation.GravityScale = 1.0f;
        definition.Simulation.Drag = 0.5f;
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);

        runtime.Update(0.5f);
        Particle particle = runtime.GetParticle(0);

        Assert.Equal(new Vector3(0.0f, 1.5f, 0.0f), particle.Velocity);
        Assert.Equal(new Vector3(0.0f, 0.75f, 0.0f), particle.Position);
    }

    [Fact]
    public void Update_AppliesLifetimeCurvesAndGradient()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Initial.Lifetime = FloatRange.Constant(2.0f);
        definition.Initial.Speed = FloatRange.Constant(0.0f);
        definition.Initial.Size = Vector2Range.Constant(new Vector2(4.0f, 6.0f));
        definition.Initial.StartColor = ColorGradient.Constant(Color.White);
        definition.Simulation.SizeOverLifetime = FloatCurve.FadeOut();
        definition.Simulation.AlphaOverLifetime = FloatCurve.FadeOut();
        definition.Simulation.ColorOverLifetime = ColorGradient.Constant(Color.Blue);
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);

        runtime.Update(1.0f);
        Particle particle = runtime.GetParticle(0);

        Assert.Equal(new Vector2(2.0f, 3.0f), particle.Size);
        Assert.Equal(Color.Blue, particle.Color);
        Assert.Equal(0.5f, particle.Alpha, 5);
    }

    [Fact]
    public void Update_KillsParticlesAtLifetimeEnd()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Initial.Lifetime = FloatRange.Constant(1.0f);
        definition.Initial.Speed = FloatRange.Constant(0.0f);
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);

        runtime.Update(0.5f);
        Assert.Equal(1, runtime.AliveCount);

        runtime.Update(0.5f);
        Assert.Equal(0, runtime.AliveCount);
    }

    [Fact]
    public void Update_DoesNotSimulateWhilePaused()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Initial.Speed = FloatRange.Constant(2.0f);
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);
        runtime.Play();
        runtime.Pause();

        runtime.Update(1.0f);

        Assert.Equal(0.0f, runtime.GetParticle(0).Age);
        Assert.Equal(Vector3.Zero, runtime.GetParticle(0).Position);
    }

    private static ParticleEmitterDefinition CreateDefinition()
    {
        var definition = new ParticleEmitterDefinition
        {
            Duration = 10.0f,
            MaxParticles = 8,
        };

        definition.Emission.RateOverTime = 0.0f;
        definition.Initial.Lifetime = FloatRange.Constant(10.0f);
        definition.Initial.Speed = FloatRange.Constant(0.0f);
        definition.Initial.Rotation = FloatRange.Constant(0.0f);
        definition.Initial.AngularVelocity = FloatRange.Constant(0.0f);
        definition.Initial.Size = Vector2Range.Constant(Vector2.One);
        definition.Initial.StartColor = ColorGradient.Constant(Color.White);
        definition.Simulation.Gravity = Vector3.Zero;
        definition.Simulation.GravityScale = 0.0f;
        definition.Simulation.Drag = 0.0f;
        definition.Simulation.SizeOverLifetime = FloatCurve.Constant(1.0f);
        definition.Simulation.AlphaOverLifetime = FloatCurve.Constant(1.0f);
        definition.Simulation.VelocityOverLifetime = FloatCurve.Constant(1.0f);
        definition.Simulation.ColorOverLifetime = ColorGradient.Constant(Color.White);
        return definition;
    }
}