using CasaEngine.Framework.Particles.Authoring;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.EditorServices.Particles;

public readonly record struct ParticleSystemComponentAttachment(SceneComponent? Parent, bool AttachAsRoot);

public static class EditorParticleSystemComponentService
{
    public static ParticleSystemComponent CreateParticleComponent(Guid particleAssetId, Vector3 position)
        => new()
        {
            Name = "Particle System",
            ParticleEffectAssetId = particleAssetId,
            Position = position,
            PlayOnStart = true,
            Looping = true,
            SimulateInEditor = true,
        };

    public static ParticleSystemComponentAttachment CreateAttachment(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.RootComponent == null
            ? new ParticleSystemComponentAttachment(null, AttachAsRoot: true)
            : new ParticleSystemComponentAttachment(entity.RootComponent, AttachAsRoot: false);
    }

    public static void AttachComponent(Entity entity, ParticleSystemComponent component, ParticleSystemComponentAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(component);

        if (attachment.AttachAsRoot)
        {
            entity.RootComponent = component;
        }
        else if (attachment.Parent != null)
        {
            attachment.Parent.AddChildComponent(component);
        }
        else
        {
            entity.AddComponent(component);
        }

        component.Initialize();
        if (entity.World != null)
        {
            component.InitializeWithWorld(entity.World);
        }
    }

    public static void DetachComponent(Entity entity, ParticleSystemComponent component, ParticleSystemComponentAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(component);

        if (attachment.AttachAsRoot && ReferenceEquals(entity.RootComponent, component))
        {
            entity.RootComponent = null;
            return;
        }

        if (attachment.Parent != null)
        {
            attachment.Parent.RemoveChildComponent(component);
            return;
        }

        entity.RemoveComponent(component);
    }

    public static void ApplyParticleAsset(Entity entity, ParticleSystemComponent component, Guid particleAssetId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(component);

        if (particleAssetId == Guid.Empty)
        {
            component.ClearParticleEffectAsset();
            return;
        }

        component.ParticleEffectAssetId = particleAssetId;
        if (entity.World?.Game?.AssetContentManager == null)
        {
            return;
        }

        ParticleEffectAsset particleAsset = entity.World.Game.AssetContentManager.Load<ParticleEffectAsset>(particleAssetId);
        component.SetParticleEffectAsset(particleAsset);
    }
}