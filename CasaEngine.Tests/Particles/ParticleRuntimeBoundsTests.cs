using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleRuntimeBoundsTests
{
    [Fact]
    public void UpdateBounds_LocalSimulation_TransformsBoundsToWorld()
    {
        ParticleEmitterDefinition definition = CreateDefinition(ParticleSimulationSpace.Local, new Vector2(4.0f, 4.0f));
        var runtime = new ParticleEmitterRuntime(definition)
        {
            WorldMatrix = Matrix.CreateTranslation(10.0f, 0.0f, 0.0f),
        };
        runtime.Emit(1);

        runtime.UpdateBounds(runtime.WorldMatrix);

        Assert.True(runtime.HasBounds);
        Assert.Equal(new Vector3(-2.0f, -2.0f, -2.0f), runtime.LocalBounds.Min);
        Assert.Equal(new Vector3(2.0f, 2.0f, 2.0f), runtime.LocalBounds.Max);
        Assert.Equal(new Vector3(8.0f, -2.0f, -2.0f), runtime.WorldBounds.Min);
        Assert.Equal(new Vector3(12.0f, 2.0f, 2.0f), runtime.WorldBounds.Max);
    }

    [Fact]
    public void Emit_WorldSimulation_TransformsParticleAtSpawnOnly()
    {
        ParticleEmitterDefinition definition = CreateDefinition(ParticleSimulationSpace.World, new Vector2(2.0f, 2.0f));
        var runtime = new ParticleEmitterRuntime(definition)
        {
            WorldMatrix = Matrix.CreateTranslation(10.0f, 0.0f, 0.0f),
        };

        runtime.Emit(1);
        Assert.Equal(new Vector3(10.0f, 0.0f, 0.0f), runtime.GetParticle(0).Position);

        runtime.UpdateBounds(Matrix.CreateTranslation(20.0f, 0.0f, 0.0f));

        Assert.True(runtime.HasBounds);
        Assert.Equal(new Vector3(9.0f, -1.0f, -1.0f), runtime.WorldBounds.Min);
        Assert.Equal(new Vector3(11.0f, 1.0f, 1.0f), runtime.WorldBounds.Max);
    }

    [Fact]
    public void RuntimeInstance_UpdateBounds_UnionsEmitterWorldBounds()
    {
        var asset = new ParticleEffectAsset();
        asset.Emitters.Add(CreateDefinition(ParticleSimulationSpace.Local, Vector2.One * 2.0f));
        asset.Emitters.Add(CreateDefinition(ParticleSimulationSpace.Local, Vector2.One * 2.0f));
        var instance = new ParticleRuntimeInstance(asset);
        instance.Emitters[0].Emit(1);
        instance.Emitters[1].Emit(1);
        ref Particle particle = ref instance.Emitters[1].GetParticle(0);
        particle.Position = new Vector3(5.0f, 0.0f, 0.0f);

        instance.UpdateBounds();

        Assert.True(instance.HasBounds);
        Assert.Equal(new Vector3(-1.0f, -1.0f, -1.0f), instance.Bounds.Min);
        Assert.Equal(new Vector3(6.0f, 1.0f, 1.0f), instance.Bounds.Max);
    }

    [Fact]
    public void Clear_InvalidatesRuntimeBounds()
    {
        ParticleEmitterDefinition definition = CreateDefinition(ParticleSimulationSpace.Local, Vector2.One * 2.0f);
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);
        runtime.UpdateBounds(Matrix.Identity);
        Assert.True(runtime.HasBounds);

        runtime.Clear();

        Assert.False(runtime.HasBounds);
        Assert.Equal(default, runtime.WorldBounds);
    }

    [Fact]
    public void AlwaysVisible_ReflectsRendererModuleFlag()
    {
        ParticleEmitterDefinition definition = CreateDefinition(ParticleSimulationSpace.Local, Vector2.One);
        definition.Renderer.AlwaysVisible = true;

        var runtime = new ParticleEmitterRuntime(definition);

        Assert.True(runtime.AlwaysVisible);
    }

    private static ParticleEmitterDefinition CreateDefinition(ParticleSimulationSpace simulationSpace, Vector2 size)
    {
        var definition = new ParticleEmitterDefinition
        {
            MaxParticles = 4,
            Duration = 10.0f,
        };

        definition.Emission.RateOverTime = 0.0f;
        definition.Initial.Lifetime = FloatRange.Constant(10.0f);
        definition.Initial.Speed = FloatRange.Constant(0.0f);
        definition.Initial.Size = Vector2Range.Constant(size);
        definition.Initial.Rotation = FloatRange.Constant(0.0f);
        definition.Initial.AngularVelocity = FloatRange.Constant(0.0f);
        definition.Initial.StartColor = ColorGradient.Constant(Color.White);
        definition.Simulation.SimulationSpace = simulationSpace;
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