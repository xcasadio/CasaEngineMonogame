using CasaEngine.Framework.Particles.Authoring;

namespace CasaEngine.Framework.Particles.Runtime;

public sealed class ParticleRuntimeInstance
{
    public ParticleEffectAsset Asset { get; }

    public ParticleEmitterRuntime[] Emitters { get; }

    public int AliveCount
    {
        get
        {
            int aliveCount = 0;
            for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
            {
                aliveCount += Emitters[emitterIndex].AliveCount;
            }

            return aliveCount;
        }
    }

    public ParticleRuntimeInstance(ParticleEffectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        Asset = asset;
        Emitters = new ParticleEmitterRuntime[asset.Emitters.Count];
        for (int emitterIndex = 0; emitterIndex < asset.Emitters.Count; emitterIndex++)
        {
            Emitters[emitterIndex] = new ParticleEmitterRuntime(asset.Emitters[emitterIndex]);
        }
    }

    public void Clear()
    {
        for (int emitterIndex = 0; emitterIndex < Emitters.Length; emitterIndex++)
        {
            Emitters[emitterIndex].Clear();
        }
    }
}