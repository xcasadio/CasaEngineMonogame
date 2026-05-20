using System.Reflection;
using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleRuntimeStabilityTests
{
    [Fact]
    public void RepeatedOneShotBursts_ReusesCapacityAndExpiresParticles()
    {
        ParticleEffectAsset asset = CreateBurstAsset(maxParticles: 64, burstCount: 12, lifetime: 0.2f);
        var runtime = new ParticleRuntimeInstance(asset);

        for (int burstIndex = 0; burstIndex < 100; burstIndex++)
        {
            runtime.Restart(clearParticles: true);
            runtime.Update(1.0f / 60.0f);

            Assert.InRange(runtime.AliveCount, 1, 64);
            Assert.Equal(64, runtime.Emitters[0].Capacity);

            runtime.Update(1.0f);

            Assert.Equal(0, runtime.AliveCount);
            Assert.Equal(64, runtime.Emitters[0].Capacity);
        }
    }

    [Fact]
    public void LongLoopingSimulation_StaysWithinEmitterCapacity()
    {
        ParticleEffectAsset asset = CreateLoopingAsset(maxParticles: 48, rateOverTime: 24.0f, lifetime: 1.0f);
        var runtime = new ParticleRuntimeInstance(asset);
        runtime.Play();

        for (int frameIndex = 0; frameIndex < 600; frameIndex++)
        {
            runtime.Update(1.0f / 60.0f);

            Assert.InRange(runtime.AliveCount, 0, 48);
            Assert.Equal(48, runtime.Emitters[0].Capacity);
        }

        Assert.True(runtime.IsAlive);
        Assert.InRange(runtime.AliveCount, 1, 48);
    }

    [Fact]
    public void ComponentDisableEnableAndRestart_ControlRuntimeSafely()
    {
        ParticleSystemComponent component = CreateAttachedComponent(out Entity entity, out World world);
        component.SetParticleEffectAsset(CreateBurstAsset(maxParticles: 16, burstCount: 6, lifetime: 1.0f));

        entity.IsEnabled = false;
        component.Update(0.1f);

        Assert.Equal(0, component.LastEmittedCount);
        Assert.Equal(0, component.RuntimeInstance!.AliveCount);

        entity.IsEnabled = true;
        component.Update(0.1f);

        Assert.InRange(component.LastEmittedCount, 1, 16);
        Assert.InRange(component.RuntimeInstance.AliveCount, 1, 16);

        component.Restart(clearParticles: true);

        Assert.Equal(0, component.RuntimeInstance.AliveCount);

        SetWorldUpdateSequence(world, 1);
        component.Update(0.1f);

        Assert.InRange(component.LastEmittedCount, 1, 16);
        Assert.InRange(component.RuntimeInstance.AliveCount, 1, 16);
    }

    private static ParticleSystemComponent CreateAttachedComponent(out Entity entity, out World world)
    {
        world = new World();
        entity = new Entity();
        var component = new ParticleSystemComponent();
        entity.RootComponent = component;
        entity.InitializeWithWorld(world);
        return component;
    }

    private static void SetWorldUpdateSequence(World world, int updateSequence)
    {
        FieldInfo? field = typeof(World).GetField("<UpdateSequence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(world, updateSequence);
    }

    private static ParticleEffectAsset CreateBurstAsset(int maxParticles, int burstCount, float lifetime)
    {
        ParticleEffectAsset asset = CreateBaseAsset(maxParticles, lifetime);
        ParticleEmitterDefinition emitter = asset.Emitters[0];
        emitter.Duration = 0.35f;
        emitter.Looping = false;
        emitter.Emission.RateOverTime = 0.0f;
        emitter.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.0f,
            CountMin = burstCount,
            CountMax = burstCount,
        });
        return asset;
    }

    private static ParticleEffectAsset CreateLoopingAsset(int maxParticles, float rateOverTime, float lifetime)
    {
        ParticleEffectAsset asset = CreateBaseAsset(maxParticles, lifetime);
        ParticleEmitterDefinition emitter = asset.Emitters[0];
        emitter.Duration = 1.0f;
        emitter.Looping = true;
        emitter.Emission.RateOverTime = rateOverTime;
        return asset;
    }

    private static ParticleEffectAsset CreateBaseAsset(int maxParticles, float lifetime)
    {
        var asset = new ParticleEffectAsset();
        var emitter = new ParticleEmitterDefinition
        {
            Duration = 1.0f,
            Looping = false,
            MaxParticles = maxParticles,
        };

        emitter.Shape.ShapeType = ParticleShapeType.Point;
        emitter.Initial.Lifetime = FloatRange.Constant(lifetime);
        emitter.Initial.Speed = FloatRange.Constant(0.1f);
        emitter.Initial.Size = Vector2Range.Constant(Vector2.One * 0.1f);
        emitter.Initial.StartColor = ColorGradient.White;
        emitter.Simulation.SimulationSpace = ParticleSimulationSpace.World;
        emitter.Simulation.Gravity = Vector3.Zero;
        emitter.Simulation.GravityScale = 0.0f;
        emitter.Simulation.Drag = 0.0f;
        emitter.Simulation.SizeOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.AlphaOverLifetime = FloatCurve.FadeOut();
        emitter.Simulation.VelocityOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.ColorOverLifetime = ColorGradient.White;
        asset.Emitters.Add(emitter);
        return asset;
    }
}