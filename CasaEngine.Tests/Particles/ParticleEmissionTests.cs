using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleEmissionTests
{
    [Fact]
    public void UpdateEmission_AccumulatesFractionalRateOverTime()
    {
        var runtime = new ParticleEmitterRuntime(CreateDefinition(rateOverTime: 2.0f));

        runtime.Play();

        Assert.Equal(0, runtime.UpdateEmission(0.25f));
        Assert.Equal(1, runtime.UpdateEmission(0.25f));
        Assert.Equal(0, runtime.UpdateEmission(0.25f));
        Assert.Equal(1, runtime.UpdateEmission(0.25f));

        Assert.Equal(2, runtime.AliveCount);
    }

    [Fact]
    public void UpdateEmission_UsesOnlyActiveTimeAfterStartDelay()
    {
        var runtime = new ParticleEmitterRuntime(CreateDefinition(rateOverTime: 10.0f, startDelay: 0.5f));

        runtime.Play();

        Assert.Equal(0, runtime.UpdateEmission(0.25f));
        Assert.Equal(0, runtime.UpdateEmission(0.25f));
        Assert.Equal(1, runtime.UpdateEmission(0.1f));

        Assert.Equal(1, runtime.AliveCount);
    }

    [Fact]
    public void UpdateEmission_BurstAtZero_TriggersOncePerLoopCycle()
    {
        var definition = CreateDefinition(rateOverTime: 0.0f, duration: 1.0f);
        definition.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.0f,
            CountMin = 1,
            CountMax = 1,
        });
        var runtime = new ParticleEmitterRuntime(definition);

        runtime.Play();
        Assert.Equal(1, runtime.UpdateEmission(0.1f));
        Assert.Equal(0, runtime.UpdateEmission(0.8f));
        Assert.Equal(1, runtime.UpdateEmission(0.2f));

        Assert.Equal(2, runtime.AliveCount);
        Assert.Equal(1, runtime.CycleIndex);
    }

    [Fact]
    public void UpdateEmission_LowFrameRate_CatchesBurstInCrossedCycles()
    {
        var definition = CreateDefinition(rateOverTime: 0.0f, duration: 1.0f);
        definition.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.25f,
            CountMin = 2,
            CountMax = 2,
        });
        var runtime = new ParticleEmitterRuntime(definition);

        runtime.Play();
        int emittedCount = runtime.UpdateEmission(1.3f);

        Assert.Equal(4, emittedCount);
        Assert.Equal(4, runtime.AliveCount);
    }

    [Fact]
    public void UpdateEmission_BurstCountRange_IsDeterministicForSameSeed()
    {
        ParticleEmitterDefinition firstDefinition = CreateDefinition(rateOverTime: 0.0f);
        ParticleEmitterDefinition secondDefinition = CreateDefinition(rateOverTime: 0.0f);
        firstDefinition.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.0f,
            CountMin = 2,
            CountMax = 5,
        });
        secondDefinition.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.0f,
            CountMin = 2,
            CountMax = 5,
        });

        var firstRuntime = new ParticleEmitterRuntime(firstDefinition, randomSeed: 123u);
        var secondRuntime = new ParticleEmitterRuntime(secondDefinition, randomSeed: 123u);

        firstRuntime.Play();
        secondRuntime.Play();

        Assert.Equal(firstRuntime.UpdateEmission(0.1f), secondRuntime.UpdateEmission(0.1f));
        Assert.Equal(firstRuntime.AliveCount, secondRuntime.AliveCount);
    }

    [Fact]
    public void UpdateEmission_RespectsParticleCapacity()
    {
        var definition = CreateDefinition(rateOverTime: 0.0f, maxParticles: 2);
        definition.Emission.Bursts.Add(new ParticleBurst
        {
            Time = 0.0f,
            CountMin = 10,
            CountMax = 10,
        });
        var runtime = new ParticleEmitterRuntime(definition);

        runtime.Play();
        int emittedCount = runtime.UpdateEmission(0.1f);

        Assert.Equal(2, emittedCount);
        Assert.Equal(2, runtime.AliveCount);
    }

    [Fact]
    public void RuntimeInstance_UpdateEmission_UpdatesAllEmitters()
    {
        var asset = new ParticleEffectAsset();
        asset.Emitters.Add(CreateDefinition(rateOverTime: 1.0f));
        asset.Emitters.Add(CreateDefinition(rateOverTime: 2.0f));
        var instance = new ParticleRuntimeInstance(asset);

        instance.Play();
        int emittedCount = instance.UpdateEmission(1.0f);

        Assert.Equal(3, emittedCount);
        Assert.Equal(3, instance.AliveCount);
    }

    private static ParticleEmitterDefinition CreateDefinition(float rateOverTime, float duration = 10.0f, float startDelay = 0.0f, int maxParticles = 32)
    {
        var definition = new ParticleEmitterDefinition
        {
            Duration = duration,
            Looping = true,
            StartDelay = startDelay,
            MaxParticles = maxParticles,
        };

        definition.Emission.RateOverTime = rateOverTime;
        return definition;
    }
}