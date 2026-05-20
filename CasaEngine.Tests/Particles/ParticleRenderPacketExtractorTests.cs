using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Particles.Rendering;
using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleRenderPacketExtractorTests
{
    [Fact]
    public void Extract_ClearsDestinationAndWritesParticleRenderData()
    {
        Guid textureAssetId = Guid.NewGuid();
        ParticleRuntimeInstance runtime = CreateRuntime(textureAssetId);
        runtime.Emit(1);
        ref Particle particle = ref runtime.Emitters[0].GetParticle(0);
        particle.Position = new Vector3(0.0f, 3.0f, 4.0f);
        particle.Size = new Vector2(2.0f, 3.0f);
        particle.Rotation = 0.25f;
        particle.Color = new Color(100, 150, 200, 255);
        particle.Alpha = 0.5f;
        var packets = new List<ParticleRenderPacket>
        {
            new(),
        };

        int packetCount = ParticleRenderPacketExtractor.Extract(runtime, Vector3.Zero, new Color(128, 255, 128, 128), packets);

        Assert.Equal(1, packetCount);
        Assert.Single(packets);
        ParticleRenderPacket packet = packets[0];
        Assert.Equal(new Vector3(0.0f, 3.0f, 4.0f), packet.Position);
        Assert.Equal(new Vector2(2.0f, 3.0f), packet.Size);
        Assert.Equal(0.25f, packet.Rotation);
        Assert.Equal(50, packet.Color.R);
        Assert.Equal(150, packet.Color.G);
        Assert.Equal(100, packet.Color.B);
        Assert.Equal(64, packet.Color.A);
        Assert.Equal(0.2509804f, packet.Alpha, 5);
        Assert.Equal(textureAssetId, packet.TextureAssetId);
        Assert.Equal(ParticleBlendMode.Additive, packet.BlendMode);
        Assert.Equal(ParticleSortMode.Distance, packet.SortMode);
        Assert.True(packet.DepthTest);
        Assert.False(packet.DepthWrite);
        Assert.Equal(3001, packet.RenderQueue);
        Assert.Equal(2, packet.Layer);
        Assert.Equal(25.0f, packet.DistanceToCameraSquared);
    }

    [Fact]
    public void Extract_ReturnsNoPacketsForDeadParticles()
    {
        ParticleRuntimeInstance runtime = CreateRuntime(Guid.Empty);
        var packets = new List<ParticleRenderPacket>();

        int packetCount = ParticleRenderPacketExtractor.Extract(runtime, Vector3.Zero, Color.White, packets);

        Assert.Equal(0, packetCount);
        Assert.Empty(packets);
    }

    private static ParticleRuntimeInstance CreateRuntime(Guid textureAssetId)
    {
        var asset = new ParticleEffectAsset();
        var emitter = new ParticleEmitterDefinition
        {
            MaxParticles = 4,
        };

        emitter.Renderer.TextureAssetId = textureAssetId;
        emitter.Renderer.BlendMode = ParticleBlendMode.Additive;
        emitter.Renderer.SortMode = ParticleSortMode.Distance;
        emitter.Renderer.RenderQueue = 3001;
        emitter.Renderer.Layer = 2;
        emitter.Renderer.DepthTest = true;
        emitter.Renderer.DepthWrite = false;
        emitter.Emission.RateOverTime = 0.0f;
        emitter.Initial.Lifetime = FloatRange.Constant(10.0f);
        emitter.Initial.Speed = FloatRange.Constant(0.0f);
        emitter.Initial.Size = Vector2Range.Constant(Vector2.One);
        emitter.Initial.StartColor = ColorGradient.Constant(Color.White);
        emitter.Simulation.Gravity = Vector3.Zero;
        emitter.Simulation.GravityScale = 0.0f;
        emitter.Simulation.SizeOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.AlphaOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.VelocityOverLifetime = FloatCurve.Constant(1.0f);
        emitter.Simulation.ColorOverLifetime = ColorGradient.Constant(Color.White);
        asset.Emitters.Add(emitter);
        return new ParticleRuntimeInstance(asset);
    }
}