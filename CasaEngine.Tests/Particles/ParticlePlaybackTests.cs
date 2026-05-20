using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Runtime;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticlePlaybackTests
{
    [Fact]
    public void PlayPauseResume_PreservesPlaybackTime()
    {
        var runtime = CreateEmitterRuntime(duration: 10.0f, looping: true, startDelay: 0.0f);

        runtime.Play();
        Assert.True(runtime.AdvancePlayback(1.0f));
        runtime.Pause();
        Assert.False(runtime.AdvancePlayback(1.0f));
        runtime.Play();
        Assert.True(runtime.AdvancePlayback(1.0f));

        Assert.Equal(ParticlePlaybackState.Playing, runtime.PlaybackState);
        Assert.Equal(2.0f, runtime.PlaybackTime);
        Assert.Equal(2.0f, runtime.CycleTime);
    }

    [Fact]
    public void AdvancePlayback_RespectsStartDelay()
    {
        var runtime = CreateEmitterRuntime(duration: 2.0f, looping: true, startDelay: 0.5f);

        runtime.Play();
        Assert.False(runtime.AdvancePlayback(0.25f));
        Assert.False(runtime.HasStarted);
        Assert.Equal(0.0f, runtime.CycleTime);

        Assert.True(runtime.AdvancePlayback(0.25f));
        Assert.True(runtime.HasStarted);
        Assert.Equal(0.0f, runtime.CycleTime);
    }

    [Fact]
    public void LoopingEmitter_WrapsCycleTimeAndIncrementsCycleIndex()
    {
        var runtime = CreateEmitterRuntime(duration: 2.0f, looping: true, startDelay: 0.0f);

        runtime.Play();
        Assert.True(runtime.AdvancePlayback(2.75f));

        Assert.Equal(ParticlePlaybackState.Playing, runtime.PlaybackState);
        Assert.Equal(1, runtime.CycleIndex);
        Assert.Equal(0.75f, runtime.CycleTime, 5);
    }

    [Fact]
    public void OneShotEmitter_StopsAfterDurationButCanKeepLiveParticles()
    {
        var runtime = CreateEmitterRuntime(duration: 1.0f, looping: false, startDelay: 0.0f);
        Assert.True(runtime.TrySpawn(out _));

        runtime.Play();
        Assert.False(runtime.AdvancePlayback(2.0f));

        Assert.Equal(ParticlePlaybackState.Stopped, runtime.PlaybackState);
        Assert.Equal(1.0f, runtime.CycleTime);
        Assert.True(runtime.IsAlive);
        Assert.Equal(1, runtime.AliveCount);
    }

    [Fact]
    public void Stop_ClearParticlesControlsAliveParticles()
    {
        var runtime = CreateEmitterRuntime(duration: 1.0f, looping: true, startDelay: 0.0f);
        Assert.True(runtime.TrySpawn(out _));

        runtime.Play();
        runtime.AdvancePlayback(0.5f);
        runtime.Stop(clearParticles: false);

        Assert.Equal(ParticlePlaybackState.Stopped, runtime.PlaybackState);
        Assert.Equal(0.0f, runtime.PlaybackTime);
        Assert.Equal(1, runtime.AliveCount);
        Assert.True(runtime.IsAlive);

        runtime.Stop(clearParticles: true);

        Assert.Equal(0, runtime.AliveCount);
        Assert.False(runtime.IsAlive);
    }

    [Fact]
    public void Restart_ClearsTimersAndOptionallyParticles()
    {
        var runtime = CreateEmitterRuntime(duration: 5.0f, looping: true, startDelay: 0.0f);
        Assert.True(runtime.TrySpawn(out _));
        runtime.Play();
        runtime.AdvancePlayback(3.0f);

        runtime.Restart(clearParticles: true);

        Assert.Equal(ParticlePlaybackState.Playing, runtime.PlaybackState);
        Assert.Equal(0.0f, runtime.PlaybackTime);
        Assert.Equal(0.0f, runtime.CycleTime);
        Assert.Equal(0, runtime.AliveCount);
    }

    [Fact]
    public void RuntimeInstance_PropagatesPlaybackAndSimulationSpeed()
    {
        var asset = new ParticleEffectAsset();
        asset.Emitters.Add(new ParticleEmitterDefinition
        {
            Duration = 5.0f,
            MaxParticles = 2,
        });
        asset.Emitters.Add(new ParticleEmitterDefinition
        {
            Duration = 5.0f,
            MaxParticles = 2,
        });

        var instance = new ParticleRuntimeInstance(asset)
        {
            SimulationSpeed = 2.0f,
        };

        instance.Play();
        instance.AdvancePlayback(0.5f);

        Assert.Equal(ParticlePlaybackState.Playing, instance.PlaybackState);
        Assert.Equal(1.0f, instance.Emitters[0].PlaybackTime);
        Assert.Equal(1.0f, instance.Emitters[1].PlaybackTime);
        Assert.True(instance.IsAlive);
    }

    private static ParticleEmitterRuntime CreateEmitterRuntime(float duration, bool looping, float startDelay)
        => new(new ParticleEmitterDefinition
        {
            Duration = duration,
            Looping = looping,
            StartDelay = startDelay,
            MaxParticles = 8,
        });
}