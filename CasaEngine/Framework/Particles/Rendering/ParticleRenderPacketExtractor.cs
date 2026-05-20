using CasaEngine.Framework.Particles.Runtime;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Rendering;

public static class ParticleRenderPacketExtractor
{
    public static int Extract(ParticleRuntimeInstance runtimeInstance, Vector3 cameraPosition, Color colorTint, List<ParticleRenderPacket> destination)
    {
        ArgumentNullException.ThrowIfNull(runtimeInstance);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        for (int emitterIndex = 0; emitterIndex < runtimeInstance.Emitters.Length; emitterIndex++)
        {
            ParticleEmitterRuntime emitter = runtimeInstance.Emitters[emitterIndex];
            ExtractEmitter(emitter, emitterIndex, cameraPosition, colorTint, destination);
        }

        return destination.Count;
    }

    private static void ExtractEmitter(ParticleEmitterRuntime emitter, int emitterIndex, Vector3 cameraPosition, Color colorTint, List<ParticleRenderPacket> destination)
    {
        ReadOnlySpan<int> aliveParticleIndices = emitter.AliveParticleIndices;
        for (int aliveIndex = 0; aliveIndex < aliveParticleIndices.Length; aliveIndex++)
        {
            int particleIndex = aliveParticleIndices[aliveIndex];
            Particle particle = emitter.Particles[particleIndex];
            if (!particle.IsAlive)
            {
                continue;
            }

            Vector3 cameraDelta = particle.Position - cameraPosition;
            var flipbook = emitter.Definition.Renderer.Flipbook;
            destination.Add(new ParticleRenderPacket
            {
                Position = particle.Position,
                Size = particle.Size,
                Rotation = particle.Rotation,
                Color = ApplyTint(particle.Color, particle.Alpha, colorTint),
                Alpha = particle.Alpha * (colorTint.A / 255.0f),
                TextureAssetId = emitter.Definition.Renderer.TextureAssetId,
                FlipbookColumns = flipbook?.Columns ?? 1,
                FlipbookRows = flipbook?.Rows ?? 1,
                FlipbookFrameIndex = particle.FlipbookFrame,
                BlendMode = emitter.Definition.Renderer.BlendMode,
                SortMode = emitter.Definition.Renderer.SortMode,
                RenderMode = emitter.Definition.Renderer.RenderMode,
                DepthTest = emitter.Definition.Renderer.DepthTest,
                DepthWrite = emitter.Definition.Renderer.DepthWrite,
                RenderQueue = emitter.Definition.Renderer.RenderQueue,
                Layer = emitter.Definition.Renderer.Layer,
                EmitterIndex = emitterIndex,
                ParticleIndex = particleIndex,
                DistanceToCameraSquared = cameraDelta.LengthSquared(),
            });
        }
    }

    private static Color ApplyTint(Color particleColor, float particleAlpha, Color colorTint)
    {
        float alpha = MathHelper.Clamp(particleAlpha * (colorTint.A / 255.0f), 0.0f, 1.0f);
        return new Color(
            (byte)(particleColor.R * colorTint.R / 255),
            (byte)(particleColor.G * colorTint.G / 255),
            (byte)(particleColor.B * colorTint.B / 255),
            (byte)(alpha * 255.0f));
    }
}