using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleRuntimeBufferTests
{
    [Fact]
    public void TrySpawn_StopsAtMaxParticles()
    {
        var runtime = new ParticleEmitterRuntime(new ParticleEmitterDefinition
        {
            MaxParticles = 2,
        });

        Assert.True(runtime.TrySpawn(out int firstIndex));
        Assert.True(runtime.TrySpawn(out int secondIndex));
        Assert.False(runtime.TrySpawn(out int failedIndex));

        Assert.Equal(0, firstIndex);
        Assert.Equal(1, secondIndex);
        Assert.Equal(-1, failedIndex);
        Assert.Equal(2, runtime.AliveCount);
    }

    [Fact]
    public void Kill_ReusesFreedParticleIndex()
    {
        var runtime = new ParticleEmitterRuntime(new ParticleEmitterDefinition
        {
            MaxParticles = 3,
        });

        Assert.True(runtime.TrySpawn(out int firstIndex));
        Assert.True(runtime.TrySpawn(out int secondIndex));

        Assert.True(runtime.Kill(firstIndex));
        Assert.False(runtime.GetParticle(firstIndex).IsAlive);
        Assert.True(runtime.GetParticle(secondIndex).IsAlive);
        Assert.True(runtime.TrySpawn(out int reusedIndex));

        Assert.Equal(firstIndex, reusedIndex);
        Assert.True(runtime.GetParticle(reusedIndex).IsAlive);
        Assert.Equal(2, runtime.AliveCount);
    }

    [Fact]
    public void Kill_InvalidOrDeadParticle_ReturnsFalse()
    {
        var runtime = new ParticleEmitterRuntime(new ParticleEmitterDefinition
        {
            MaxParticles = 1,
        });

        Assert.False(runtime.Kill(0));
        Assert.False(runtime.Kill(-1));
        Assert.False(runtime.Kill(1));
    }

    [Fact]
    public void Clear_ResetsAliveParticlesAndFreeList()
    {
        var runtime = new ParticleEmitterRuntime(new ParticleEmitterDefinition
        {
            MaxParticles = 2,
        });

        Assert.True(runtime.TrySpawn(out int firstIndex));
        Assert.True(runtime.TrySpawn(out _));

        runtime.Clear();

        Assert.Equal(0, runtime.AliveCount);
        Assert.False(runtime.GetParticle(firstIndex).IsAlive);
        Assert.True(runtime.TrySpawn(out int resetIndex));
        Assert.Equal(0, resetIndex);
    }

    [Fact]
    public void ParticleRuntimeInstance_PreallocatesEmitterBuffers()
    {
        var asset = new ParticleEffectAsset();
        asset.Emitters.Add(new ParticleEmitterDefinition
        {
            MaxParticles = 2,
        });
        asset.Emitters.Add(new ParticleEmitterDefinition
        {
            MaxParticles = 5,
        });

        var instance = new ParticleRuntimeInstance(asset);

        Assert.Equal(2, instance.Emitters.Length);
        Assert.Equal(2, instance.Emitters[0].Capacity);
        Assert.Equal(5, instance.Emitters[1].Capacity);
        Assert.Equal(0, instance.AliveCount);
    }
}