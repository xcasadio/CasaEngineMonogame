using CasaEngine.Framework.Particles.Authoring;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleEffectAssetTests
{
    [Fact]
    public void Validate_WithoutEmitters_ReturnsError()
    {
        var asset = new ParticleEffectAsset
        {
            Name = "EmptyEffect",
        };

        var errors = asset.Validate();

        Assert.Contains("Particle effect 'EmptyEffect' must contain at least one emitter.", errors);
    }

    [Fact]
    public void Validate_WithDefaultEmitter_ReturnsNoErrors()
    {
        var asset = new ParticleEffectAsset();
        asset.Emitters.Add(new ParticleEmitterDefinition());

        var errors = asset.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithInvalidEmitter_ReturnsModuleErrors()
    {
        var asset = new ParticleEffectAsset();
        asset.Emitters.Add(new ParticleEmitterDefinition
        {
            Name = "Broken",
            Duration = -1.0f,
            StartDelay = float.NaN,
            MaxParticles = 0,
            Shape = new ParticleShapeModule
            {
                Radius = -1.0f,
                AngleDegrees = 270.0f,
                Size = new Vector3(-1.0f, 1.0f, 1.0f),
            },
            Simulation = new ParticleSimulationModule
            {
                Drag = -0.5f,
            },
            Renderer = new ParticleRendererModule
            {
                RenderQueue = -1,
            },
        });

        var errors = asset.Validate();

        Assert.Contains("Emitter 'Broken' duration must be greater than zero.", errors);
        Assert.Contains("Emitter 'Broken' start delay must be finite and non-negative.", errors);
        Assert.Contains("Emitter 'Broken' max particles must be greater than zero.", errors);
        Assert.Contains("Emitter 'Broken' shape radius must be finite and non-negative.", errors);
        Assert.Contains("Emitter 'Broken' shape angle must be between 0 and 180 degrees.", errors);
        Assert.Contains("Emitter 'Broken' shape size must be finite and non-negative.", errors);
        Assert.Contains("Emitter 'Broken' drag must be finite and non-negative.", errors);
        Assert.Contains("Emitter 'Broken' render queue must be non-negative.", errors);
    }

    [Fact]
    public void ParticleBurst_NormalizesCountRange()
    {
        var burst = new ParticleBurst
        {
            CountMin = 10,
            CountMax = 5,
        };

        Assert.Equal(5, burst.CountMin);
        Assert.Equal(5, burst.CountMax);
    }
}