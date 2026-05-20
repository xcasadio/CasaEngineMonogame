using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Rendering;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleFlipbookTests
{
    [Fact]
    public void ResolveFlipbookFrame_UsesLifetimeCurveWhenFpsIsZero()
    {
        var flipbook = new ParticleFlipbookModule
        {
            Columns = 4,
            Rows = 2,
            FrameCount = 8,
            FrameOverLifetime = FloatCurve.FadeIn(),
        };

        int frame = ParticleEmitterRuntime.ResolveFlipbookFrame(flipbook, 0, 0.25f, 0.5f);

        Assert.Equal(4, frame);
    }

    [Fact]
    public void ResolveFlipbookFrame_UsesFpsAndWrapsFromRandomStartFrame()
    {
        var flipbook = new ParticleFlipbookModule
        {
            Columns = 2,
            Rows = 2,
            FrameCount = 4,
            FramesPerSecond = 8.0f,
        };

        int frame = ParticleEmitterRuntime.ResolveFlipbookFrame(flipbook, 3, 0.25f, 0.0f);

        Assert.Equal(1, frame);
    }

    [Fact]
    public void Update_AdvancesParticleFlipbookFrame()
    {
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Renderer.Flipbook.Columns = 4;
        definition.Renderer.Flipbook.Rows = 1;
        definition.Renderer.Flipbook.FrameCount = 4;
        definition.Renderer.Flipbook.FramesPerSecond = 4.0f;
        var runtime = new ParticleEmitterRuntime(definition);
        runtime.Emit(1);

        runtime.Update(0.5f);

        Assert.Equal(2, runtime.GetParticle(0).FlipbookFrame);
    }

    [Fact]
    public void Extract_WritesFlipbookDataToRenderPacket()
    {
        var asset = new ParticleEffectAsset();
        ParticleEmitterDefinition definition = CreateDefinition();
        definition.Renderer.Flipbook.Columns = 3;
        definition.Renderer.Flipbook.Rows = 2;
        definition.Renderer.Flipbook.FrameCount = 6;
        asset.Emitters.Add(definition);
        var runtime = new ParticleRuntimeInstance(asset);
        runtime.Emit(1);
        ref Particle particle = ref runtime.Emitters[0].GetParticle(0);
        particle.FlipbookFrame = 5;
        var packets = new List<ParticleRenderPacket>();

        ParticleRenderPacketExtractor.Extract(runtime, Vector3.Zero, Color.White, packets);

        Assert.Single(packets);
        Assert.Equal(3, packets[0].FlipbookColumns);
        Assert.Equal(2, packets[0].FlipbookRows);
        Assert.Equal(5, packets[0].FlipbookFrameIndex);
    }

    [Fact]
    public void Renderer_CalculatesUvRectangleFromFlipbookFrame()
    {
        var packet = new ParticleRenderPacket
        {
            FlipbookColumns = 4,
            FlipbookRows = 2,
            FlipbookFrameIndex = 5,
        };

        ParticleRendererComponent.GetFlipbookTextureCoordinates(
            packet,
            out Vector2 uvTopLeft,
            out Vector2 uvTopRight,
            out Vector2 uvBottomRight,
            out Vector2 uvBottomLeft);

        Assert.Equal(new Vector2(0.25f, 0.5f), uvTopLeft);
        Assert.Equal(new Vector2(0.5f, 0.5f), uvTopRight);
        Assert.Equal(new Vector2(0.5f, 1.0f), uvBottomRight);
        Assert.Equal(new Vector2(0.25f, 1.0f), uvBottomLeft);
    }

    private static ParticleEmitterDefinition CreateDefinition()
    {
        var definition = new ParticleEmitterDefinition
        {
            Duration = 10.0f,
            MaxParticles = 4,
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