using CasaEngine.Framework.Common;
using CasaEngine.Framework.Particles.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Serializable authoring asset describing a complete particle effect.
/// </summary>
public sealed class ParticleEffectAsset : ObjectBase
{
    public const int CurrentVersion = 2;

    public ParticleEffectAsset()
    {
        Name = $"Particle Effect {Id}";
    }

    public int Version { get; set; } = CurrentVersion;

    public List<ParticleEmitterDefinition> Emitters { get; } = new();

    public override void Load(JObject element)
    {
        base.Load(element);
        ParticleEffectAssetJsonSerializer.Load(this, element);
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Version <= 0)
        {
            errors.Add("Particle effect version must be greater than zero.");
        }

        if (Emitters.Count == 0)
        {
            errors.Add($"Particle effect '{Name}' must contain at least one emitter.");
        }

        for (int emitterIndex = 0; emitterIndex < Emitters.Count; emitterIndex++)
        {
            ParticleEmitterDefinition? emitter = Emitters[emitterIndex];
            if (emitter == null)
            {
                errors.Add($"Particle effect '{Name}' has a null emitter at index {emitterIndex}.");
                continue;
            }

            emitter.Validate(errors, emitterIndex);
        }

        return errors.Count == 0 ? Array.Empty<string>() : errors.ToArray();
    }
}